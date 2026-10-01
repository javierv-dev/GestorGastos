#!/usr/bin/env python
"""Puebla la base de datos de desarrollo de GestorGastos y prueba todos los endpoints.

Todo pasa por la API HTTP real, así que los datos respetan las reglas del dominio.
Además de las transacciones crea presupuestos mensuales de ejemplo (el límite de cada categoría sale
del gasto real: máximo mensual x 1,10 redondeado a la decena, mínimo 50; una categoría queda
"ajustada" para poder demostrar el rechazo 409 de la regla del presupuesto) y prueba sus endpoints.
Usa solo la biblioteca estándar de Python.

Ejemplos de uso:
    python scripts/poblar_datos.py                      # poblar, crear presupuestos y probar (aborta si ya hay datos)
    python scripts/poblar_datos.py --dry-run            # muestra el plan de datos y de presupuestos sin escribir nada
    python scripts/poblar_datos.py --solo-presupuestos  # añade presupuestos a una base ya poblada (no destructivo)
    python scripts/poblar_datos.py --solo-datos --agregar
    python scripts/poblar_datos.py --solo-datos --limpiar --si   # borra presupuestos y transacciones, y repuebla
    python scripts/poblar_datos.py --solo-datos --sin-presupuestos
    python scripts/poblar_datos.py --solo-pruebas       # prueba los endpoints sin dejar residuos
    python scripts/poblar_datos.py --semilla 7 --url http://localhost:5007

Para arrancar la API:
    dotnet run --project GestorGastos.Api --launch-profile http

Códigos de salida: 0 todo bien, 1 alguna prueba o creación falló,
2 se abortó por datos existentes o falta de confirmación, 3 la API no responde.
"""

import argparse
import calendar
import json
import random
import socket
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import date, datetime, time, timedelta
from decimal import ROUND_CEILING, Decimal

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")

DEFAULT_URL = "http://localhost:5007"  # perfil "http" de launchSettings.json
REQUEST_TIMEOUT = 15
LOW_BALANCE_THRESHOLD = Decimal("100")
TEST_PREFIX = "[PRUEBA]"
CATEGORIES = [
    "Alimentacion", "Transporte", "Vivienda", "Servicios", "Salud",
    "Entretenimiento", "Educacion", "Salario", "Otros",
]  # mismo orden que el enum CategoriaTransaccion
START_DATE = date(2026, 7, 1)
END_DATE = date(2026, 9, 28)
CENT = Decimal("0.01")
BUDGET_FREE_CATEGORIES = ("Educacion", "Otros")  # categorías que quedan sin presupuesto a propósito
BUDGET_FACTOR = Decimal("1.10")  # holgura sobre el máximo gasto mensual
BUDGET_MIN = Decimal("50")
TIGHT_MARGIN = Decimal("15.00")  # margen de la categoría "ajustada" (entre 10 y 20)
HIGHLIGHT_MONTH = date(2026, 9, 1)  # mes que se muestra en la tabla y se usa en la demostración
BUDGET_EXCEEDED_TITLE = "Presupuesto.Excedido"


# ---------------------------------------------------------------- cliente HTTP

class ApiUnavailable(Exception):
    """La API no respondió (conexión rechazada o tiempo de espera agotado)."""


class Resp:
    """Respuesta HTTP simplificada."""

    def __init__(self, status, text, headers):
        self.status = status
        self.text = text
        self.headers = headers

    @property
    def json(self):
        if not self.text:
            return None
        return json.loads(self.text, parse_float=Decimal)


def _encode(value):
    if isinstance(value, Decimal):
        return float(value)
    raise TypeError(f"No serializable: {type(value)}")


class Client:
    def __init__(self, base_url):
        self.base_url = base_url.rstrip("/")

    def call(self, method, path, body=None, query=None):
        url = self.base_url + path
        if query:
            url += "?" + urllib.parse.urlencode(query)
        data = json.dumps(body, default=_encode).encode("utf-8") if body is not None else None
        req = urllib.request.Request(
            url, data=data, method=method,
            headers={"Content-Type": "application/json", "Accept": "application/json"},
        )
        try:
            with urllib.request.urlopen(req, timeout=REQUEST_TIMEOUT) as r:
                return Resp(r.status, r.read().decode("utf-8"), r.headers)
        except urllib.error.HTTPError as e:
            return Resp(e.code, e.read().decode("utf-8", "replace"), e.headers)
        except (urllib.error.URLError, socket.timeout, ConnectionError, TimeoutError) as e:
            raise ApiUnavailable(str(e)) from e

    def list_all(self, **query):
        r = self.call("GET", "/transacciones", query=query or None)
        if r.status != 200:
            raise RuntimeError(f"GET /transacciones devolvió {r.status}")
        return r.json

    def balance(self):
        r = self.call("GET", "/transacciones/saldo")
        if r.status != 200:
            raise RuntimeError(f"GET /transacciones/saldo devolvió {r.status}")
        return Decimal(str(r.json["saldo"]))

    def list_budgets(self):
        r = self.call("GET", "/presupuestos")
        if r.status != 200:
            raise RuntimeError(f"GET /presupuestos devolvió {r.status}")
        return r.json

    def summary(self):
        r = self.call("GET", "/transacciones/resumen")
        if r.status != 200:
            raise RuntimeError(f"GET /transacciones/resumen devolvió {r.status}")
        return r.json


def dec(value):
    return Decimal(str(value))


def iso(moment):
    return moment.strftime("%Y-%m-%dT%H:%M:%S")


def fmt(amount):
    return f"{amount:,.2f}"


def month_key(moment):
    """Clave 'AAAA-MM' de una fecha, de un datetime o de un texto ISO."""
    return str(moment)[:7]


def month_bounds(first_day):
    """Primer y último día del mes de una fecha, como texto ISO (para los filtros desde/hasta)."""
    last = first_day.replace(day=calendar.monthrange(first_day.year, first_day.month)[1])
    return first_day.replace(day=1).isoformat(), last.isoformat()


def is_budget_rejection(resp):
    """True si la respuesta es el 409 de la regla del presupuesto (title Presupuesto.Excedido)."""
    if resp.status != 409:
        return False
    try:
        return resp.json.get("title") == BUDGET_EXCEEDED_TITLE
    except Exception:
        return False


# ------------------------------------------------------------ generador de datos

def build_plan(seed):
    """Genera la lista cronológica de transacciones. Misma semilla, mismos datos."""
    rng = random.Random(seed)

    def money(low, high):
        return Decimal(str(round(rng.uniform(low, high), 2))).quantize(CENT)

    def skewed(low, high, mode):
        return Decimal(str(round(rng.triangular(low, high, mode), 2))).quantize(CENT)

    def at(day, hour_from=7, hour_to=22):
        return datetime.combine(day, time(rng.randint(hour_from, hour_to), rng.randint(0, 59), rng.randint(0, 59)))

    events = []  # cada evento: fecha, descripcion, monto, tipo, categoria, essential, dip

    def add(moment, description, amount, kind, category, essential=False):
        events.append({
            "fecha": moment, "descripcion": description, "monto": amount, "tipo": kind,
            "categoria": category, "essential": essential, "dip": None,
        })

    rent = Decimal(rng.choice([820, 850, 880]))

    for year, month in [(2026, 7), (2026, 8), (2026, 9)]:
        salary_day = rng.choice([1, 5])
        salary = Decimal(rng.randrange(240000, 260001, 500)) / 100
        add(at(date(year, month, salary_day), 8, 9), "Salario mensual", salary, "Ingreso", "Salario")
        add(at(date(year, month, salary_day + 2), 10, 13), "Alquiler del apartamento", rent, "Egreso", "Vivienda", True)
        add(at(date(year, month, 8), 9, 12), "Plan de internet hogar", Decimal("39.90"), "Egreso", "Servicios", True)
        add(at(date(year, month, 12), 9, 18), "Factura de electricidad", money(45, 90), "Egreso", "Servicios", True)
        add(at(date(year, month, 16), 9, 18), "Factura de agua", money(18, 35), "Egreso", "Servicios", True)
        add(at(date(year, month, 20), 9, 18), "Plan de telefonía móvil", money(22, 28), "Egreso", "Servicios", True)
        add(at(date(year, month, 14), 6, 9), "Suscripción de streaming de video", Decimal("11.99"), "Egreso", "Entretenimiento", True)
        add(at(date(year, month, 14), 10, 12), "Suscripción de música", Decimal("7.99"), "Egreso", "Entretenimiento", True)
        for _ in range(2):
            add(at(date(year, month, rng.randint(2, 27)), 8, 20),
                rng.choice(["Combustible en Gasolinera Norte", "Combustible en Estación Sur"]),
                money(30, 60), "Egreso", "Transporte")
        for _ in range(rng.randint(1, 2)):
            add(at(date(year, month, rng.randint(2, 27)), 7, 10), "Recarga de tarjeta de transporte",
                money(10, 20), "Egreso", "Transporte")

    # Gastos diarios con probabilidad por día
    stores = ["Supermercado Central", "Mercado Municipal", "Verdulería La Esquina", "Panadería San José",
              "Carnicería Don Mario", "Minimercado Express"]
    dining = [("Café en Cafetería La Plaza", 4, 12), ("Almuerzo en Restaurante El Fogón", 18, 40),
              ("Cena en Pizzería Il Forno", 22, 55), ("Desayuno en Panadería San José", 5, 14)]
    outings = ["Salida con amigos en el bar La Terraza", "Entradas de cine en Cine Plaza", "Noche de bolos en Boliche Central"]
    misc = [("Peluquería", 10, 25), ("Regalo de cumpleaños", 15, 50), ("Lavandería", 8, 15),
            ("Artículos de ferretería", 6, 30), ("Donación a fundación", 10, 20), ("Papelería", 3, 12)]
    day = START_DATE
    while day <= END_DATE:
        weekend = day.weekday() >= 5
        if rng.random() < (0.50 if weekend else 0.30):
            add(at(day, 9, 20), f"Compra en {rng.choice(stores)}", skewed(15, 120, 45), "Egreso", "Alimentacion")
        if rng.random() < 0.15:
            name, low, high = rng.choice(dining)
            add(at(day, 8, 22), name, money(low, high), "Egreso", "Alimentacion")
        if rng.random() < 0.10:
            add(at(day, 7, 22), "Viaje en app de transporte", money(4, 18), "Egreso", "Transporte")
        if rng.random() < 0.05:
            add(at(day, 9, 20), "Compra en Farmacia del Barrio", money(6, 40), "Egreso", "Salud")
        if rng.random() < 0.05:
            outing = rng.choice(outings)
            add(at(day, 17, 23), outing, money(8, 25) if "cine" in outing else money(20, 60), "Egreso", "Entretenimiento")
        if rng.random() < 0.04:
            name, low, high = rng.choice(misc)
            add(at(day, 9, 20), name, money(low, high), "Egreso", "Otros")
        day += timedelta(days=1)

    # Eventos puntuales
    add(at(date(2026, 8, 6 + rng.randint(0, 6)), 9, 18), "Reparación de llave y sifón del baño", money(35, 90), "Egreso", "Vivienda")
    add(at(date(2026, 9, 10 + rng.randint(0, 5)), 9, 18), "Materiales de pintura para la sala", money(25, 70), "Egreso", "Vivienda")
    add(at(date(2026, 8, 18 + rng.randint(0, 6)), 9, 17), "Consulta médica general", money(45, 70), "Egreso", "Salud")
    add(at(date(2026, 9, 7 + rng.randint(0, 6)), 9, 17), "Limpieza dental en Clínica Sonrisa", money(50, 80), "Egreso", "Salud")
    add(at(date(2026, 7, 8), 19, 22), "Curso en línea de Excel avanzado", Decimal("49.90"), "Egreso", "Educacion")
    add(at(date(2026, 8, 20), 10, 18), "Libros de texto en Librería Pensar", money(25, 45), "Egreso", "Educacion")
    add(at(date(2026, 9, 11), 19, 22), "Suscripción a plataforma de cursos", Decimal("19.99"), "Egreso", "Educacion")
    add(at(date(2026, 9, 22), 10, 18), "Libro técnico de programación", money(22, 38), "Egreso", "Educacion")
    add(at(date(2026, 7, 17), 10, 18), "Trabajo freelance - diseño de logotipo", money(180, 350), "Ingreso", "Otros")
    add(at(date(2026, 8, 22), 10, 18), "Trabajo freelance - mantenimiento de sitio web", money(120, 300), "Ingreso", "Otros")
    add(at(date(2026, 8, 9), 10, 18), "Venta de artículo usado - bicicleta", money(60, 140), "Ingreso", "Otros")
    add(at(date(2026, 8, 14), 10, 18), "Reembolso de gastos médicos del seguro", money(25, 60), "Ingreso", "Salud")
    add(at(date(2026, 9, 14), 10, 18), "Reembolso por compra devuelta", money(20, 45), "Ingreso", "Otros")
    add(at(date(2026, 9, 27), 9, 11), "Trabajo freelance - pago de proyecto web", money(380, 620), "Ingreso", "Otros")

    # Momentos deliberados de saldo bajo: un egreso grande deja el saldo entre 40 y 95
    for moment, description, category in [
        (datetime(2026, 7, 30, 20, 30, 0), "Reparación del auto en Taller Mecánico Rápido", "Transporte"),
        (datetime(2026, 8, 29, 18, 40, 0), "Tratamiento dental y medicamentos", "Salud"),
        (datetime(2026, 9, 26, 19, 10, 0), "Compra de lavadora nueva", "Vivienda"),
    ]:
        events.append({"fecha": moment, "descripcion": description, "monto": None, "tipo": "Egreso",
                       "categoria": category, "essential": True, "dip": money(40, 95)})

    events = [e for _, e in sorted(enumerate(events), key=lambda p: (p[1]["fecha"], p[0]))
              if START_DATE <= e["fecha"].date() <= END_DATE]

    # Simulación del saldo: descarta gastos que lo dejarían negativo
    plan = []
    balance = Decimal("0")
    for ev in events:
        ev["dip_deliberate"] = False
        if ev["dip"] is not None:
            lump = balance - ev["dip"]
            if not (Decimal("10") <= lump <= Decimal("1800")):
                continue
            ev["monto"] = lump
            ev["dip_deliberate"] = True
        elif ev["tipo"] == "Egreso":
            margin = Decimal("0") if ev["essential"] else Decimal("5")
            if balance - ev["monto"] < margin:
                continue
        balance += ev["monto"] if ev["tipo"] == "Ingreso" else -ev["monto"]
        plan.append(ev)

    # Ajuste final para que el saldo termine entre 300 y 2.500
    closing = datetime(2026, 9, 28, 21, 0, 0)
    if balance < 300:
        extra = (Decimal("450") - balance).quantize(CENT)
        plan.append({"fecha": closing, "descripcion": "Trabajo freelance - bono de cierre", "monto": extra,
                     "tipo": "Ingreso", "categoria": "Otros", "dip_deliberate": False})
    elif balance > 2500:
        extra = (balance - Decimal("2000")).quantize(CENT)
        plan.append({"fecha": closing, "descripcion": "Compra de electrodoméstico", "monto": extra,
                     "tipo": "Egreso", "categoria": "Vivienda", "dip_deliberate": False})
    return plan


def summarize_plan(plan):
    """Devuelve (conteo por categoría y tipo, saldo final, saldos bajos esperados, momentos deliberados)."""
    counts = {c: {"Ingreso": 0, "Egreso": 0} for c in CATEGORIES}
    balance = Decimal("0")
    low = 0
    for ev in plan:
        counts[ev["categoria"]][ev["tipo"]] += 1
        if ev["tipo"] == "Ingreso":
            balance += ev["monto"]
        else:
            balance -= ev["monto"]
            if balance < LOW_BALANCE_THRESHOLD:
                low += 1
    deliberate = sum(1 for ev in plan if ev["dip_deliberate"])
    return counts, balance, low, deliberate


def print_plan(plan):
    counts, balance, low, deliberate = summarize_plan(plan)
    print(f"Plan de datos: {len(plan)} transacciones entre {plan[0]['fecha']:%Y-%m-%d} y {plan[-1]['fecha']:%Y-%m-%d}")
    print(f"{'Categoría':<18}{'Ingresos':>10}{'Egresos':>10}")
    for category in CATEGORIES:
        c = counts[category]
        if c["Ingreso"] or c["Egreso"]:
            print(f"{category:<18}{c['Ingreso']:>10}{c['Egreso']:>10}")
    print(f"Saldo final esperado: {fmt(balance)}")
    print(f"Egresos que dejan el saldo bajo {LOW_BALANCE_THRESHOLD} (saldoBajo esperado): {low} "
          f"({deliberate} momentos deliberados)")


# ------------------------------------------------------------ presupuestos de ejemplo

def expenses_of_transactions(transactions):
    """Egresos como tuplas (categoría, mes 'AAAA-MM', monto) a partir de lo que devuelve la API."""
    return [(x["categoria"], month_key(x["fecha"]), dec(x["monto"])) for x in transactions if x["tipo"] == "Egreso"]


def expenses_of_plan(plan):
    """Lo mismo, a partir del plan generado en memoria (para --dry-run)."""
    return [(ev["categoria"], month_key(ev["fecha"].date()), ev["monto"]) for ev in plan if ev["tipo"] == "Egreso"]


def round_up_limit(peak):
    """Máximo gasto mensual x 1,10 redondeado hacia arriba a la decena, con mínimo 50."""
    limit = (peak * BUDGET_FACTOR / 10).to_integral_value(rounding=ROUND_CEILING) * 10
    return max(limit, BUDGET_MIN)


def build_budget_rows(expenses, existing):
    """Calcula los presupuestos a crear. Devuelve (filas, categorías omitidas por ya tener presupuesto).

    Ningún mes histórico supera su límite, salvo la categoría "ajustada" (límite = gasto del mes
    destacado + margen), que se elige entre las que tienen gasto en ese mes y prefiere una cuyo mes
    destacado sea además su máximo, para que el resto del historial siga siendo coherente.
    """
    spend = {}
    for category, month, amount in expenses:
        months = spend.setdefault(category, {})
        months[month] = months.get(month, Decimal("0")) + amount
    highlight = month_key(HIGHLIGHT_MONTH)

    rows, skipped = [], []
    for category in CATEGORIES:
        if category == "Salario" or category in BUDGET_FREE_CATEGORIES or category not in spend:
            continue
        if category in existing:
            skipped.append(category)
            continue
        rows.append({"categoria": category, "limite": round_up_limit(max(spend[category].values())),
                     "gastado": spend[category].get(highlight, Decimal("0")), "ajustada": False})

    def preference(row):
        is_peak = row["gastado"] == max(spend[row["categoria"]].values())
        return (not is_peak, row["categoria"] != "Entretenimiento")

    candidates = [r for r in rows if r["gastado"] > 0]
    if candidates:
        tight = min(candidates, key=preference)  # a igual preferencia gana el primero del enum
        tight["limite"] = tight["gastado"] + TIGHT_MARGIN
        tight["ajustada"] = True
    return rows, skipped


def print_budget_table(rows):
    month = HIGHLIGHT_MONTH.strftime("%m/%Y")
    print(f"  {'Categoría':<18}{'Límite':>12}{'Gastado ' + month:>18}{'% uso':>9}")
    for row in rows:
        usage = row["gastado"] / row["limite"] * 100
        mark = "  <- ajustada" if row["ajustada"] else ""
        print(f"  {row['categoria']:<18}{fmt(row['limite']):>12}{fmt(row['gastado']):>18}{usage:>8.1f}%{mark}")


def create_budgets(client, rows):
    """Crea cada presupuesto por la API, guarda el id en la fila y devuelve la cantidad de errores."""
    errors = 0
    for row in rows:
        r = client.call("POST", "/presupuestos", {"categoria": row["categoria"], "limiteMensual": row["limite"]})
        if r.status == 201 and r.headers.get("Location"):
            row["id"] = r.json["id"]
        else:
            errors += 1
            print(f"  ERROR al crear el presupuesto de {row['categoria']}: HTTP {r.status} {r.text[:200]}")
    return errors


def demo_rule(client, row):
    """Intenta un egreso que excede el presupuesto ajustado y comprueba el 409. No deja residuos."""
    category = row["categoria"]
    first, last = month_bounds(HIGHLIGHT_MONTH)
    spent = sum((dec(x["monto"]) for x in client.list_all(categoria=category, desde=first, hasta=last)
                 if x["tipo"] == "Egreso"), Decimal("0"))
    amount = row["limite"] - spent + Decimal("1.00")  # 1,00 por encima de lo disponible
    count_before, balance_before = len(client.list_all()), client.balance()
    body = {"descripcion": f"{TEST_PREFIX} Demostración de la regla del presupuesto", "monto": amount,
            "tipo": "Egreso", "categoria": category, "fecha": f"{HIGHLIGHT_MONTH:%Y-%m}-15T12:00:00"}
    r = client.call("POST", "/transacciones", body)
    if r.status == 201:  # no debería ocurrir: se borra lo creado
        client.call("DELETE", f"/transacciones/{r.json['id']}")
    rejected = is_budget_rejection(r)
    clean = len(client.list_all()) == count_before and client.balance() == balance_before
    ok = rejected and clean
    print(f"  Demostración de la regla: {'PASA' if ok else 'FALLA'} "
          f"(egreso de {fmt(amount)} en {category}: HTTP {r.status}"
          f"{', sin residuos' if clean else ', QUEDARON RESIDUOS'})")
    if rejected:
        print(f"    {r.json.get('detail')}")
    return ok


def seed_budgets(client):
    """Crea los presupuestos de ejemplo a partir del gasto real de la API. Devuelve 0 o 1."""
    expenses = expenses_of_transactions(client.list_all())
    existing = {b["categoria"] for b in client.list_budgets()}
    rows, skipped = build_budget_rows(expenses, existing)
    print("Presupuestos de ejemplo (calculados con el gasto real de la API)")
    if skipped:
        print(f"  Omitidas por tener ya presupuesto: {', '.join(skipped)}")
    if not rows:
        print("  No hay presupuestos nuevos que crear.")
        return 0
    errors = create_budgets(client, rows)
    print_budget_table(rows)
    print(f"  Presupuestos creados: {len(rows) - errors} de {len(rows)}")
    tight = next((r for r in rows if r["ajustada"] and "id" in r), None)
    demo_ok = demo_rule(client, tight) if tight else True
    return 1 if errors or not demo_ok else 0


def print_budget_plan(expenses, existing=()):
    """Muestra qué presupuestos se crearían (modo --dry-run)."""
    rows, skipped = build_budget_rows(expenses, set(existing))
    print()
    print("Plan de presupuestos (límite = máximo gasto mensual x 1,10 a la decena, mínimo 50)")
    if skipped:
        print(f"  Se omitirían por tener ya presupuesto: {', '.join(skipped)}")
    if rows:
        print_budget_table(rows)
    else:
        print("  No hay presupuestos nuevos que crear.")
    print(f"  Sin presupuesto a propósito: {', '.join(BUDGET_FREE_CATEGORIES)} (y Salario, que no es presupuestable)")


def budgets_only(client):
    """Modo --solo-presupuestos: no toca transacciones. Devuelve un código de salida."""
    if not client.list_all():
        print("ABORTADO: la API no tiene transacciones, no hay gasto real con el que calcular los límites.")
        return 2
    return seed_budgets(client)


# ----------------------------------------------------------------------- poblar

def confirm_cleanup(tx_count, budget_count, assume_yes):
    print(f"ATENCIÓN: se van a borrar TODOS los presupuestos ({budget_count}) y TODAS las transacciones ({tx_count}).")
    if assume_yes:
        return True
    if not sys.stdin.isatty():
        print("Error: sin terminal interactiva hace falta pasar --si para confirmar el borrado.")
        return False
    return input("Escribe 'si' para continuar: ").strip().lower() in ("si", "sí")


def delete_all(client, path, items):
    """Borra cada elemento por la API. Devuelve True si todos se borraron."""
    for item in items:
        r = client.call("DELETE", f"{path}/{item['id']}")
        if r.status != 204:
            print(f"Error al borrar {item['id']}: HTTP {r.status}")
            return False
    return True


def populate(client, args):
    """Crea el plan en la API. Devuelve un código de salida (0, 1 o 2)."""
    plan = build_plan(args.semilla)
    existing = client.list_all()
    budgets = client.list_budgets()
    if existing or budgets:
        found = f"{len(existing)} transacciones y {len(budgets)} presupuestos"
        if args.limpiar:
            if not confirm_cleanup(len(existing), len(budgets), args.si):
                return 2
            # Primero los presupuestos y luego las transacciones.
            if not delete_all(client, "/presupuestos", budgets):
                return 1
            print(f"Se borraron {len(budgets)} presupuestos.")
            if not delete_all(client, "/transacciones", existing):
                return 1
            print(f"Se borraron {len(existing)} transacciones.")
        elif args.agregar:
            print(f"La API ya tiene {found}; se agregan los datos de ejemplo a lo existente.")
        else:
            print(f"ABORTADO: la API ya contiene {found} y no se tocó nada.")
            print("Opciones:")
            print("  --agregar              añade las transacciones de ejemplo a lo existente")
            print("  --solo-presupuestos    añade solo los presupuestos que falten (no destructivo)")
            print("  --limpiar --si         borra TODOS los presupuestos y transacciones, y luego puebla")
            print("  --solo-pruebas         solo prueba los endpoints sin poblar")
            return 2

    running = client.balance()
    created = errors = low_seen = mismatches = rejected = 0
    print(f"Creando {len(plan)} transacciones en orden cronológico...")
    for i, ev in enumerate(plan, 1):
        body = {"descripcion": ev["descripcion"], "monto": ev["monto"], "tipo": ev["tipo"],
                "categoria": ev["categoria"], "fecha": iso(ev["fecha"])}
        r = client.call("POST", "/transacciones", body)
        if is_budget_rejection(r):  # solo ocurre con --agregar sobre presupuestos ya creados
            rejected += 1
            if rejected <= 5:
                print(f"  Rechazada por presupuesto en #{i} ({ev['descripcion']}): {r.json.get('detail')}")
            continue
        if r.status != 201 or not r.headers.get("Location"):
            errors += 1
            print(f"  ERROR en #{i} ({ev['descripcion']}): HTTP {r.status} {r.text[:200]}")
            continue
        created += 1
        running += ev["monto"] if ev["tipo"] == "Ingreso" else -ev["monto"]
        expected_low = ev["tipo"] == "Egreso" and running < LOW_BALANCE_THRESHOLD
        got_low = bool(r.json.get("saldoBajo"))
        low_seen += got_low
        if got_low != expected_low:
            mismatches += 1
            print(f"  DISCREPANCIA saldoBajo en #{i} ({ev['descripcion']}): esperado {expected_low}, recibido {got_low}")
        if i % 25 == 0:
            print(f"  {i}/{len(plan)}")

    final_balance = client.balance()
    print()
    print("Resumen de la población")
    print(f"  Transacciones creadas: {created} de {len(plan)}")
    print(f"  Rechazadas por presupuesto: {rejected}")
    print(f"  Errores: {errors}")
    print(f"  Veces con saldoBajo=true: {low_seen} (discrepancias con lo esperado: {mismatches})")
    print(f"  Saldo final (API): {fmt(final_balance)} | esperado: {fmt(running)}")
    print()
    print(f"  {'Categoría':<18}{'Ingresos':>12}{'Egresos':>12}{'Neto':>12}")
    for row in client.summary():
        ing, egr = dec(row["totalIngresos"]), dec(row["totalEgresos"])
        print(f"  {row['categoria']:<18}{fmt(ing):>12}{fmt(egr):>12}{fmt(ing - egr):>12}")
    bad = errors or mismatches or final_balance != running
    budget_failed = 0
    if not args.sin_presupuestos:
        print()
        budget_failed = seed_budgets(client)
    return 1 if bad or budget_failed else 0


# ---------------------------------------------------------------------- pruebas

class Tester:
    def __init__(self, client):
        self.client = client
        self.passed = self.failed = self.skipped = 0
        self.created_ids = []
        self.budget_ids = []

    def check(self, name, ok, expected=None, received=None):
        if ok:
            self.passed += 1
            print(f"PASA   {name}")
        else:
            self.failed += 1
            print(f"FALLA  {name}\n         esperado: {expected}\n         recibido: {received}")
        return ok

    def skip(self, name, reason):
        self.skipped += 1
        print(f"OMITIDA {name}: {reason}")

    def post(self, description, amount, kind="Egreso", category="Otros", moment=None, raw=None):
        """Crea una transacción temporal y registra su id para limpiarla al final."""
        if raw is not None:
            body = raw
        else:
            body = {
                "descripcion": f"{TEST_PREFIX} {description}" if description else description,
                "monto": amount, "tipo": kind, "fecha": iso(moment or datetime(2031, 3, 10, 12, 0, 0)),
            }
            if category is not None:
                body["categoria"] = category
        r = self.client.call("POST", "/transacciones", body)
        if r.status == 201:
            self.created_ids.append(r.json["id"])
        return r

    def get(self, tx_id):
        return self.client.call("GET", f"/transacciones/{tx_id}")

    def has_error_field(self, r, field):
        try:
            errors = r.json["errors"]
        except Exception:
            return False
        return any(k.lower() == field.lower() for k in errors)

    def post_budget(self, category, limit):
        """Crea un presupuesto temporal y registra su id para limpiarlo al final."""
        r = self.client.call("POST", "/presupuestos", {"categoria": category, "limiteMensual": limit})
        if r.status == 201:
            self.budget_ids.append(r.json["id"])
        return r

    def free_category(self):
        """Primera categoría presupuestable que ahora mismo no tiene presupuesto (o None)."""
        taken = {b["categoria"] for b in self.client.list_budgets()}
        order = list(BUDGET_FREE_CATEGORIES) + [c for c in CATEGORIES if c not in BUDGET_FREE_CATEGORIES]
        return next((c for c in order if c != "Salario" and c not in taken), None)

    def quiet_months(self, category, count):
        """Primeros `count` meses lejanos sin egresos de la categoría, para que el gasto existente no cuente."""
        candidates = [date(2026, m, 1) for m in range(1, 7)] + [date(2025, m, 1) for m in range(12, 0, -1)]
        found = []
        for first in candidates:
            since, until = month_bounds(first)
            items = self.client.list_all(categoria=category, desde=since, hasta=until)
            if not any(x["tipo"] == "Egreso" for x in items):
                found.append(first)
                if len(found) == count:
                    break
        return found

    def cleanup(self):
        for budget_id in self.budget_ids:
            r = self.client.call("DELETE", f"/presupuestos/{budget_id}")
            if r.status not in (204, 404):
                print(f"  Aviso: no se pudo borrar el presupuesto {budget_id} (HTTP {r.status})")
        self.budget_ids.clear()
        for tx_id in self.created_ids:
            r = self.client.call("DELETE", f"/transacciones/{tx_id}")
            if r.status not in (204, 404):
                print(f"  Aviso: no se pudo borrar {tx_id} (HTTP {r.status})")
        self.created_ids.clear()


def run_tests(client):
    t = Tester(client)
    base_ids = sorted(x["id"] for x in client.list_all())
    base_balance = client.balance()
    base_budgets = sorted((b["id"], b["categoria"], dec(b["limiteMensual"])) for b in client.list_budgets())
    print(f"Estado inicial: {len(base_ids)} transacciones, {len(base_budgets)} presupuestos, "
          f"saldo {fmt(base_balance)}\n")

    sections = [test_create, test_validation, test_low_balance, test_get_by_id, test_filters,
                test_balance_and_summary, test_update, test_delete, test_budget_crud, test_budget_rule]
    try:
        for section in sections:
            print(f"--- {section.__name__} ---")
            try:
                section(t)
            except ApiUnavailable:
                raise
            except Exception as e:  # una sección rota no debe ocultar las demás
                t.check(f"{section.__name__} sin excepciones", False, "sin excepción", repr(e))
    finally:
        t.cleanup()

    print("\n--- limpieza ---")
    after_ids = sorted(x["id"] for x in client.list_all())
    after_balance = client.balance()
    t.check("La lista de transacciones quedó idéntica (mismos ids)", after_ids == base_ids,
            f"{len(base_ids)} transacciones", f"{len(after_ids)} transacciones")
    t.check("El saldo quedó exactamente como antes", after_balance == base_balance,
            fmt(base_balance), fmt(after_balance))
    after_budgets = sorted((b["id"], b["categoria"], dec(b["limiteMensual"])) for b in client.list_budgets())
    t.check("La lista de presupuestos quedó idéntica (mismos ids, categorías y límites)",
            after_budgets == base_budgets, f"{len(base_budgets)} presupuestos", f"{len(after_budgets)} presupuestos")
    print(f"\nResumen: {t.passed} pasan, {t.failed} fallan, {t.skipped} omitidas")
    return 0 if t.failed == 0 else 1


def test_create(t):
    moment = datetime(2031, 3, 10, 12, 30, 0)
    before = t.client.balance()
    r = t.post("Compra válida", Decimal("12.50"), "Egreso", "Alimentacion", moment)
    t.check("POST válido devuelve 201", r.status == 201, 201, r.status)
    body = r.json if r.status == 201 else {}
    tx_id = body.get("id")
    t.check("POST válido incluye Location con el id",
            tx_id is not None and r.headers.get("Location", "").endswith(f"/transacciones/{tx_id}"),
            f"/transacciones/{tx_id}", r.headers.get("Location"))
    t.check("POST válido: cuerpo con los datos enviados",
            body.get("descripcion") == f"{TEST_PREFIX} Compra válida" and dec(body.get("monto", -1)) == Decimal("12.50")
            and body.get("tipo") == "Egreso" and body.get("categoria") == "Alimentacion"
            and str(body.get("fecha", "")).startswith("2031-03-10T12:30:00") and "saldoBajo" in body,
            "campos enviados y saldoBajo", body)
    t.check("POST válido: el saldo baja exactamente el monto", t.client.balance() == before - Decimal("12.50"),
            fmt(before - Decimal("12.50")), fmt(t.client.balance()))

    r = t.post("Sin categoría", Decimal("3.00"), "Egreso", None, moment)
    t.check("POST sin categoría devuelve 201", r.status == 201, 201, r.status)
    t.check("POST sin categoría se guarda como Otros", r.status == 201 and r.json.get("categoria") == "Otros",
            "Otros", r.json.get("categoria") if r.status == 201 else r.status)


def test_validation(t):
    before_count = len(t.client.list_all())
    cases = [
        ("monto 0", t.post("Monto cero", Decimal("0")), "Monto"),
        ("monto negativo", t.post("Monto negativo", Decimal("-5")), "Monto"),
        ("descripción vacía", t.post("", Decimal("5")), "Descripcion"),
        ("tipo numérico 99", t.post(None, None, raw={"descripcion": f"{TEST_PREFIX} Tipo 99", "monto": 5, "tipo": 99,
                                                      "fecha": "2031-03-10T12:00:00"}), "Tipo"),
    ]
    for name, r, field in cases:
        t.check(f"POST con {name} devuelve 400", r.status == 400, 400, r.status)
        t.check(f"POST con {name} reporta el campo {field}", t.has_error_field(r, field),
                f"errors.{field}", r.text[:200])
    r = t.post(None, None, raw={"descripcion": f"{TEST_PREFIX} Tipo texto", "monto": 5, "tipo": "Inexistente",
                                "fecha": "2031-03-10T12:00:00"})
    t.check("POST con tipo de texto inexistente devuelve 400", r.status == 400, 400, r.status)
    now_count = len(t.client.list_all())
    t.check("Los POST inválidos no crearon nada", now_count == before_count, before_count, now_count)


def test_low_balance(t):
    c = t.client
    # Se sube el saldo con un ingreso temporal para poder llegar al borde exacto con seguridad.
    top_up = t.post("Ingreso para prueba de saldo bajo", Decimal("1000"), "Ingreso")
    t.check("Ingreso temporal de 1000 devuelve 201 y saldoBajo=false",
            top_up.status == 201 and top_up.json["saldoBajo"] is False, "201 / false", top_up.status)
    current = c.balance()
    if current > Decimal("1000000"):
        t.skip("Borde exacto del saldo bajo", "el saldo actual es demasiado alto para forzar el borde con seguridad")
        return
    r = t.post("Egreso que deja el saldo en 100.01", current - Decimal("100.01"))
    t.check("Egreso que deja saldo 100.01 -> saldoBajo=false", r.status == 201 and r.json["saldoBajo"] is False,
            False, r.json.get("saldoBajo") if r.status == 201 else r.status)
    r = t.post("Egreso que deja el saldo en 100.00 exacto", Decimal("0.01"))
    t.check("Saldo final exactamente 100 -> saldoBajo=false",
            r.status == 201 and r.json["saldoBajo"] is False and c.balance() == Decimal("100"),
            "false con saldo 100.00", (r.json.get("saldoBajo"), fmt(c.balance())) if r.status == 201 else r.status)
    r = t.post("Egreso que deja el saldo en 99.99", Decimal("0.01"))
    t.check("Saldo final 99.99 -> saldoBajo=true", r.status == 201 and r.json["saldoBajo"] is True,
            True, r.json.get("saldoBajo") if r.status == 201 else r.status)
    r = t.post("Egreso que deja el saldo en 50", Decimal("49.99"))
    t.check("Saldo final 50 -> saldoBajo=true", r.status == 201 and r.json["saldoBajo"] is True,
            True, r.json.get("saldoBajo") if r.status == 201 else r.status)
    r = t.post("Ingreso con el saldo aún bajo", Decimal("10"), "Ingreso")
    t.check("Un ingreso nunca avisa (saldo 60, saldoBajo=false)",
            r.status == 201 and r.json["saldoBajo"] is False and c.balance() == Decimal("60"),
            "false con saldo 60.00", (r.json.get("saldoBajo"), fmt(c.balance())) if r.status == 201 else r.status)


def test_get_by_id(t):
    r = t.post("Para GET por id", Decimal("7.25"), "Egreso", "Salud")
    tx_id = r.json["id"]
    got = t.get(tx_id)
    t.check("GET por id existente devuelve 200 con el mismo registro",
            got.status == 200 and got.json["id"] == tx_id and dec(got.json["monto"]) == Decimal("7.25")
            and got.json["categoria"] == "Salud", "200 y mismo registro", (got.status, got.text[:150]))
    missing = t.get(str(uuid.uuid4()))
    t.check("GET por id inexistente devuelve 404", missing.status == 404, 404, missing.status)
    bad = t.client.call("GET", "/transacciones/no-es-un-guid")
    t.check("GET con texto que no es guid devuelve 404", bad.status == 404, 404, bad.status)


def test_filters(t):
    def day(d, h=12, m=0, s=0):
        return datetime(2031, 4, d, h, m, s)

    a = t.post("Filtro A (día anterior 23:59:59)", Decimal("1.11"), "Egreso", "Salud", day(9, 23, 59, 59)).json["id"]
    b = t.post("Filtro B (día 10 00:00:00)", Decimal("2.22"), "Egreso", "Salud", day(10, 0, 0, 0)).json["id"]
    c_ = t.post("Filtro C (día 10 23:59:59)", Decimal("3.33"), "Ingreso", "Educacion", day(10, 23, 59, 59)).json["id"]
    d = t.post("Filtro D (día siguiente 00:00:00)", Decimal("4.44"), "Egreso", "Educacion", day(11, 0, 0, 0)).json["id"]

    def ids(**query):
        return {x["id"] for x in t.client.list_all(**query)}

    ours = {a, b, c_, d}
    got = ids(categoria="Salud")
    t.check("Filtro categoria=Salud incluye las propias y excluye otras categorías",
            {a, b} <= got and not ({c_, d} & got), "A y B sin C ni D", sorted(got & ours))
    only_health = t.client.list_all(categoria="Salud")
    t.check("Filtro categoria=Salud solo devuelve Salud", all(x["categoria"] == "Salud" for x in only_health),
            "todas Salud", {x["categoria"] for x in only_health})
    got = ids(desde="2031-04-10", hasta="2031-04-10")
    t.check("desde y hasta el mismo día devuelven el día completo (incluye 23:59:59)", got == {b, c_},
            "B y C", sorted(got))
    got = ids(desde="2031-04-10")
    t.check("Filtro desde=2031-04-10 incluye 00:00:00 y excluye el día anterior", {b, c_, d} <= got and a not in got,
            "B, C, D sin A", sorted(got & ours))
    got = ids(hasta="2031-04-10")
    t.check("Filtro hasta=2031-04-10 incluye todo el día y excluye el siguiente", {a, b, c_} <= got and d not in got,
            "A, B, C sin D", sorted(got & ours))
    got = ids(desde="2031-04-10", hasta="2031-04-11", categoria="Educacion")
    t.check("Filtros combinados (desde, hasta y categoria)", got == {c_, d}, "C y D", sorted(got))
    got = ids(desde="2031-04-10", hasta="2031-04-10", categoria="Educacion")
    t.check("Filtros combinados acotados a un día", got == {c_}, "C", sorted(got))


def test_balance_and_summary(t):
    c = t.client
    balance_before = c.balance()
    summary_before = {row["categoria"]: row for row in c.summary()}
    income, expense = Decimal("1234.56"), Decimal("78.90")
    t.post("Ingreso de resumen", income, "Ingreso", "Educacion")
    t.post("Egreso de resumen", expense, "Egreso", "Educacion")
    expected = balance_before + income - expense
    t.check("GET saldo: diferencia exacta tras un ingreso y un egreso", c.balance() == expected,
            fmt(expected), fmt(c.balance()))

    r = c.call("GET", "/transacciones/resumen")
    rows = r.json
    t.check("GET resumen devuelve 200 y una lista", r.status == 200 and isinstance(rows, list), "200 y lista", r.status)
    shape_ok = all(set(row) >= {"categoria", "totalIngresos", "totalEgresos"} and row["categoria"] in CATEGORIES
                   for row in rows)
    t.check("GET resumen: cada fila tiene categoria, totalIngresos y totalEgresos", shape_ok, "forma válida", rows[:2])
    order = [CATEGORIES.index(row["categoria"]) for row in rows if row["categoria"] in CATEGORIES]
    t.check("GET resumen: ordenado según el enum de categorías", order == sorted(order),
            "orden ascendente del enum", order)
    after = {row["categoria"]: row for row in rows}
    prev = summary_before.get("Educacion", {"totalIngresos": 0, "totalEgresos": 0})
    row = after.get("Educacion")
    t.check("GET resumen: Educacion aparece con los totales esperados",
            row is not None and dec(row["totalIngresos"]) == dec(prev["totalIngresos"]) + income
            and dec(row["totalEgresos"]) == dec(prev["totalEgresos"]) + expense,
            f"ingresos +{income}, egresos +{expense}", row)


def test_update(t):
    c = t.client
    tx_id = t.post("Para actualizar", Decimal("20.00"), "Egreso", "Transporte", datetime(2031, 3, 12, 9, 0, 0)).json["id"]
    new_body = {"descripcion": f"{TEST_PREFIX} Actualizada", "monto": Decimal("55.75"), "tipo": "Ingreso",
                "categoria": "Salud", "fecha": "2031-03-13T18:45:00"}
    r = c.call("PUT", f"/transacciones/{tx_id}", new_body)
    t.check("PUT válido devuelve 204", r.status == 204, 204, r.status)
    g = t.get(tx_id).json
    t.check("PUT válido: los cambios son visibles",
            g["descripcion"] == f"{TEST_PREFIX} Actualizada" and dec(g["monto"]) == Decimal("55.75")
            and g["tipo"] == "Ingreso" and g["categoria"] == "Salud" and g["fecha"].startswith("2031-03-13T18:45:00"),
            new_body, g)
    r = c.call("PUT", f"/transacciones/{tx_id}",
               dict(new_body, monto=Decimal("-1"), descripcion=f"{TEST_PREFIX} No debe verse"))
    t.check("PUT inválido devuelve 400 con el campo Monto", r.status == 400 and t.has_error_field(r, "Monto"),
            "400 / Monto", (r.status, r.text[:150]))
    g2 = t.get(tx_id).json
    t.check("PUT inválido no modifica el registro", g2 == g, g, g2)
    r = c.call("PUT", f"/transacciones/{uuid.uuid4()}", new_body)
    t.check("PUT de un id inexistente devuelve 404", r.status == 404, 404, r.status)


def test_delete(t):
    c = t.client
    tx_id = t.post("Para eliminar", Decimal("9.99"), "Egreso", "Otros").json["id"]
    r = c.call("DELETE", f"/transacciones/{tx_id}")
    t.check("DELETE devuelve 204", r.status == 204, 204, r.status)
    again = c.call("DELETE", f"/transacciones/{tx_id}")
    t.check("DELETE repetido devuelve 404", again.status == 404, 404, again.status)
    after = t.get(tx_id)
    t.check("GET posterior al DELETE devuelve 404", after.status == 404, 404, after.status)
    never = c.call("DELETE", f"/transacciones/{uuid.uuid4()}")
    t.check("DELETE de un id que nunca existió devuelve 404", never.status == 404, 404, never.status)


def budget_body_ok(body, category, limit):
    return (body is not None and body.get("categoria") == category and dec(body.get("limiteMensual", -1)) == limit
            and bool(body.get("id")))


def test_budget_crud(t):
    c = t.client
    category = t.free_category()
    if category is None:
        t.skip("Endpoints de presupuestos (CRUD)", "no hay ninguna categoría libre (sin presupuesto) con la que probar")
        return
    print(f"  (categoría libre elegida: {category})")
    limit = Decimal("123.45")

    r = t.post_budget(category, limit)
    body = r.json if r.status == 201 else {}
    budget_id = body.get("id")
    t.check("POST presupuesto válido devuelve 201", r.status == 201, 201, r.status)
    t.check("POST presupuesto válido incluye Location con el id",
            budget_id is not None and r.headers.get("Location", "").endswith(f"/presupuestos/{budget_id}"),
            f"/presupuestos/{budget_id}", r.headers.get("Location"))
    t.check("POST presupuesto válido: cuerpo con id, categoria y limiteMensual", budget_body_ok(body, category, limit),
            f"{category} / {limit}", body)
    if budget_id is None:
        t.skip("Resto de pruebas de presupuestos", "no se pudo crear el presupuesto base")
        return

    def count_for(cat):
        return sum(1 for b in c.list_budgets() if b["categoria"] == cat)

    r = c.call("POST", "/presupuestos", {"categoria": category, "limiteMensual": Decimal("999")})
    problem = r.json if r.text else {}
    t.check("POST duplicado devuelve 409", r.status == 409, 409, r.status)
    t.check("POST duplicado: title Presupuesto.YaExiste", problem.get("title") == "Presupuesto.YaExiste",
            "Presupuesto.YaExiste", problem.get("title"))
    t.check("POST duplicado no crea otro presupuesto", count_for(category) == 1, 1, count_for(category))

    for name, value in (("cero", Decimal("0")), ("negativo", Decimal("-10"))):
        r = c.call("POST", "/presupuestos", {"categoria": category, "limiteMensual": value})
        t.check(f"POST con límite {name} devuelve 400", r.status == 400, 400, r.status)
        t.check(f"POST con límite {name} reporta el campo LimiteMensual", t.has_error_field(r, "LimiteMensual"),
                "errors.LimiteMensual", r.text[:200])
    r = c.call("POST", "/presupuestos", {"categoria": "Salario", "limiteMensual": Decimal("100")})
    t.check("POST con categoría Salario devuelve 400 con el campo Categoria",
            r.status == 400 and t.has_error_field(r, "Categoria"), "400 / Categoria", (r.status, r.text[:150]))
    r = c.call("POST", "/presupuestos", {"categoria": "Inventada", "limiteMensual": Decimal("100")})
    t.check("POST con categoría inexistente (texto) devuelve 400", r.status == 400, 400, r.status)

    r = c.call("GET", "/presupuestos")
    rows = r.json if r.status == 200 else []
    t.check("GET lista devuelve 200 y contiene el presupuesto creado",
            r.status == 200 and any(b["id"] == budget_id for b in rows), "200 y el creado", r.status)
    order = [CATEGORIES.index(b["categoria"]) for b in rows if b["categoria"] in CATEGORIES]
    t.check("GET lista: ordenada por categoría", order == sorted(order), "orden ascendente", order)

    r = c.call("GET", f"/presupuestos/{budget_id}")
    t.check("GET por id existente devuelve 200 con el mismo presupuesto",
            r.status == 200 and budget_body_ok(r.json, category, limit), "200 y mismo registro", (r.status, r.text[:150]))
    r = c.call("GET", f"/presupuestos/{uuid.uuid4()}")
    t.check("GET por id inexistente devuelve 404", r.status == 404, 404, r.status)

    r = c.call("PUT", f"/presupuestos/{budget_id}", {"limiteMensual": Decimal("200.00")})
    t.check("PUT válido devuelve 204", r.status == 204, 204, r.status)
    got = c.call("GET", f"/presupuestos/{budget_id}").json
    t.check("PUT válido: el cambio es visible y la categoría no cambia",
            dec(got["limiteMensual"]) == Decimal("200") and got["categoria"] == category, f"{category} / 200", got)
    r = c.call("PUT", f"/presupuestos/{budget_id}", {"limiteMensual": Decimal("0")})
    t.check("PUT con límite inválido devuelve 400 con el campo LimiteMensual",
            r.status == 400 and t.has_error_field(r, "LimiteMensual"), "400 / LimiteMensual", (r.status, r.text[:150]))
    got2 = c.call("GET", f"/presupuestos/{budget_id}").json
    t.check("PUT inválido no modifica el presupuesto", got2 == got, got, got2)
    r = c.call("PUT", f"/presupuestos/{uuid.uuid4()}", {"limiteMensual": Decimal("100")})
    t.check("PUT de un presupuesto inexistente devuelve 404", r.status == 404, 404, r.status)

    r = c.call("DELETE", f"/presupuestos/{budget_id}")
    t.check("DELETE presupuesto devuelve 204", r.status == 204, 204, r.status)
    r = c.call("GET", f"/presupuestos/{budget_id}")
    t.check("GET posterior al DELETE devuelve 404", r.status == 404, 404, r.status)
    r = c.call("DELETE", f"/presupuestos/{budget_id}")
    t.check("DELETE repetido devuelve 404", r.status == 404, 404, r.status)


def test_budget_rule(t):
    c = t.client
    category = t.free_category()
    if category is None:
        t.skip("Regla del presupuesto", "no hay ninguna categoría libre (sin presupuesto) con la que probar")
        return
    months = t.quiet_months(category, 2)
    if len(months) < 2:
        t.skip("Regla del presupuesto", f"no se encontraron dos meses lejanos sin egresos de {category}")
        return
    print(f"  (categoría libre elegida: {category}; meses de prueba: {months[0]:%Y-%m} y {months[1]:%Y-%m})")
    here = datetime.combine(months[0].replace(day=15), time(12, 0, 0))
    other = datetime.combine(months[1].replace(day=15), time(12, 0, 0))
    since, until = month_bounds(months[0])
    limit = Decimal("100.00")

    budget = t.post_budget(category, limit)
    if not t.check("Presupuesto temporal de 100 creado (201)", budget.status == 201, 201, budget.status):
        return
    budget_id = budget.json["id"]

    def spend(amount, moment=here, kind="Egreso"):
        return t.post("Regla del presupuesto", amount, kind, category, moment)

    def month_count():
        return len(c.list_all(categoria=category, desde=since, hasta=until))

    r = spend(Decimal("60"))
    t.check("Egreso dentro del límite (60 de 100) devuelve 201", r.status == 201, 201, r.status)
    first_id = r.json["id"] if r.status == 201 else None

    count_before, balance_before = month_count(), c.balance()
    r = spend(Decimal("50"))
    problem = r.json if r.text else {}
    t.check("Egreso que excede (60 + 50 > 100) devuelve 409", r.status == 409, 409, r.status)
    t.check("409: title Presupuesto.Excedido", problem.get("title") == BUDGET_EXCEEDED_TITLE,
            BUDGET_EXCEEDED_TITLE, problem.get("title"))
    expected_detail = (f"El egreso excede el presupuesto mensual de {category}: "
                       "límite 100.00, ya gastado 60.00, disponible 40.00.")
    t.check("409: detail con límite, gastado y disponible correctos", problem.get("detail") == expected_detail,
            expected_detail, problem.get("detail"))
    t.check("El egreso rechazado no se guardó (ni cambió el saldo)",
            month_count() == count_before and c.balance() == balance_before,
            f"{count_before} registros, saldo {fmt(balance_before)}", (month_count(), fmt(c.balance())))

    r = spend(Decimal("40"))
    t.check("Egreso que llega EXACTAMENTE al límite (60 + 40 = 100) devuelve 201", r.status == 201, 201, r.status)
    exact_id = r.json["id"] if r.status == 201 else None
    r = spend(Decimal("0.01"))
    t.check("El siguiente egreso de 0,01 devuelve 409", r.status == 409, 409, r.status)

    r = spend(Decimal("5000"), kind="Ingreso")
    t.check("Un ingreso en la misma categoría devuelve 201 aunque supere el límite", r.status == 201, 201, r.status)
    r = spend(Decimal("0.01"))
    t.check("El ingreso no consumió ni liberó presupuesto (0,01 sigue dando 409)", r.status == 409, 409, r.status)

    r = spend(Decimal("100"), other)
    t.check("Egreso en OTRO mes devuelve 201 (el gasto de otros meses no cuenta)", r.status == 201, 201, r.status)

    if exact_id is not None:
        r = c.call("PUT", f"/transacciones/{exact_id}",
                   {"descripcion": f"{TEST_PREFIX} Editada", "monto": Decimal("50"), "tipo": "Egreso",
                    "categoria": category, "fecha": iso(here)})
        t.check("PUT que haría exceder (60 + 50 > 100) devuelve 409 Presupuesto.Excedido",
                is_budget_rejection(r), f"409 {BUDGET_EXCEEDED_TITLE}", (r.status, r.text[:150]))
        got = t.get(exact_id).json
        t.check("PUT rechazado no modifica la transacción",
                dec(got["monto"]) == Decimal("40") and got["descripcion"] == f"{TEST_PREFIX} Regla del presupuesto",
                "monto 40 y descripción original", got)

        r = c.call("DELETE", f"/transacciones/{exact_id}")
        t.check("Eliminar un egreso devuelve 204", r.status == 204, 204, r.status)
        r = spend(Decimal("40"))
        t.check("Eliminar un egreso liberó presupuesto (40 vuelve a entrar)", r.status == 201, 201, r.status)
        if r.status == 201:
            c.call("DELETE", f"/transacciones/{r.json['id']}")  # deja solo el egreso de 60

    if first_id is not None:
        r = c.call("PUT", f"/transacciones/{first_id}",
                   {"descripcion": f"{TEST_PREFIX} Editada", "monto": Decimal("100"), "tipo": "Egreso",
                    "categoria": category, "fecha": iso(here)})
        t.check("PUT de un egreso propio hasta el límite (60 -> 100) devuelve 204 (no se cuenta a sí mismo)",
                r.status == 204, 204, (r.status, r.text[:150]))
        r = spend(Decimal("0.01"))
        t.check("Con el egreso propio en 100, 0,01 más devuelve 409", r.status == 409, 409, r.status)

    r = c.call("PUT", f"/presupuestos/{budget_id}", {"limiteMensual": Decimal("150")})
    t.check("Subir el límite a 150 devuelve 204", r.status == 204, 204, r.status)
    r = spend(Decimal("50"))
    t.check("Subir el límite permite lo antes rechazado (50 más)", r.status == 201, 201, r.status)

    r = c.call("DELETE", f"/presupuestos/{budget_id}")
    t.check("Eliminar el presupuesto devuelve 204", r.status == 204, 204, r.status)
    r = spend(Decimal("1000"))
    t.check("Sin presupuesto ya no hay límite (egreso de 1000 devuelve 201)", r.status == 201, 201, r.status)


# ------------------------------------------------------------------------- main

def parse_args():
    p = argparse.ArgumentParser(
        description="Puebla y prueba la API de GestorGastos (transacciones y presupuestos).",
        epilog="ejemplos:\n"
               "  python scripts/poblar_datos.py --dry-run\n"
               "  python scripts/poblar_datos.py --solo-presupuestos\n"
               "  python scripts/poblar_datos.py --solo-datos --limpiar --si\n"
               "  python scripts/poblar_datos.py --solo-pruebas\n"
               "códigos de salida: 0 todo bien, 1 alguna prueba o creación falló, 2 se abortó\n"
               "por datos existentes o falta de confirmación, 3 la API no responde.",
        formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--url", default=DEFAULT_URL, help=f"URL base de la API (por defecto {DEFAULT_URL})")
    mode = p.add_mutually_exclusive_group()
    mode.add_argument("--solo-datos", action="store_true", help="solo poblar datos de ejemplo (y sus presupuestos)")
    mode.add_argument("--solo-pruebas", action="store_true", help="solo probar los endpoints")
    mode.add_argument("--solo-presupuestos", action="store_true",
                      help="no toca transacciones: crea los presupuestos de ejemplo a partir del gasto real, "
                           "omitiendo las categorías que ya tienen uno (no destructivo)")
    safety = p.add_mutually_exclusive_group()
    safety.add_argument("--agregar", action="store_true",
                        help="añade las transacciones aunque la API ya tenga datos (los presupuestos solo se crean si faltan)")
    safety.add_argument("--limpiar", action="store_true",
                        help="borra TODOS los presupuestos y luego TODAS las transacciones antes de poblar")
    p.add_argument("--si", action="store_true", help="confirma --limpiar sin preguntar")
    p.add_argument("--sin-presupuestos", action="store_true", help="al poblar, no crea los presupuestos de ejemplo")
    p.add_argument("--semilla", type=int, default=2026, help="semilla del generador aleatorio (por defecto 2026)")
    p.add_argument("--dry-run", action="store_true",
                   help="muestra el plan de datos y de presupuestos sin escribir en la API")
    args = p.parse_args()
    if args.solo_presupuestos and args.sin_presupuestos:
        p.error("--solo-presupuestos y --sin-presupuestos se contradicen")
    return args


def main():
    args = parse_args()
    if args.dry_run and args.solo_pruebas:
        print("--dry-run solo aplica a la población de datos; no hay nada que mostrar con --solo-pruebas.")
        return 0
    if args.dry_run and not args.solo_presupuestos:
        plan = build_plan(args.semilla)
        print_plan(plan)
        if not args.sin_presupuestos:
            print_budget_plan(expenses_of_plan(plan))
        return 0

    client = Client(args.url)
    try:
        client.balance()
    except (ApiUnavailable, RuntimeError) as e:
        print(f"No se pudo contactar con la API en {args.url}: {e}")
        print("Arráncala con:  dotnet run --project GestorGastos.Api --launch-profile http")
        return 3

    try:
        if args.solo_presupuestos:
            if args.dry_run:  # solo lectura: calcula con lo que ya hay en la API
                transactions = client.list_all()
                print(f"La API tiene {len(transactions)} transacciones (solo lectura, no se crea nada).")
                print_budget_plan(expenses_of_transactions(transactions),
                                  {b["categoria"] for b in client.list_budgets()})
                return 0
            return budgets_only(client)
        code = 0
        if not args.solo_pruebas:
            code = populate(client, args)
            if code == 2:
                return code
            print()
        if not args.solo_datos:
            code = max(code, run_tests(client))
        return code
    except ApiUnavailable as e:
        print(f"La API dejó de responder: {e}")
        return 3


if __name__ == "__main__":
    sys.exit(main())
