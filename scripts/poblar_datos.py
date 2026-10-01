#!/usr/bin/env python
"""Puebla la base de datos de desarrollo de GestorGastos y prueba todos los endpoints.

Todo pasa por la API HTTP real, así que los datos respetan las reglas del dominio.
Usa solo la biblioteca estándar de Python.

Ejemplos de uso:
    python scripts/poblar_datos.py                      # poblar y luego probar (aborta si ya hay datos)
    python scripts/poblar_datos.py --dry-run            # muestra el plan de datos sin escribir nada
    python scripts/poblar_datos.py --solo-datos --agregar
    python scripts/poblar_datos.py --solo-datos --limpiar --si
    python scripts/poblar_datos.py --solo-pruebas       # prueba los endpoints sin dejar residuos
    python scripts/poblar_datos.py --semilla 7 --url http://localhost:5007

Para arrancar la API:
    dotnet run --project GestorGastos.Api --launch-profile http

Códigos de salida: 0 todo bien, 1 alguna prueba o creación falló,
2 se abortó por datos existentes o falta de confirmación, 3 la API no responde.
"""

import argparse
import json
import random
import socket
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import date, datetime, time, timedelta
from decimal import Decimal

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


# ----------------------------------------------------------------------- poblar

def confirm_cleanup(count, assume_yes):
    print(f"ATENCIÓN: se van a borrar TODAS las transacciones existentes ({count}).")
    if assume_yes:
        return True
    if not sys.stdin.isatty():
        print("Error: sin terminal interactiva hace falta pasar --si para confirmar el borrado.")
        return False
    return input("Escribe 'si' para continuar: ").strip().lower() in ("si", "sí")


def populate(client, args):
    """Crea el plan en la API. Devuelve un código de salida (0, 1 o 2)."""
    plan = build_plan(args.semilla)
    existing = client.list_all()
    if existing:
        if args.limpiar:
            if not confirm_cleanup(len(existing), args.si):
                return 2
            for item in existing:
                r = client.call("DELETE", f"/transacciones/{item['id']}")
                if r.status != 204:
                    print(f"Error al borrar {item['id']}: HTTP {r.status}")
                    return 1
            print(f"Se borraron {len(existing)} transacciones.")
        elif args.agregar:
            print(f"La API ya tiene {len(existing)} transacciones; se agregan los datos de ejemplo a lo existente.")
        else:
            print(f"ABORTADO: la API ya contiene {len(existing)} transacciones y no se tocó nada.")
            print("Opciones:")
            print("  --agregar            añade los datos de ejemplo a lo existente")
            print("  --limpiar --si       borra TODAS las transacciones y luego puebla")
            print("  --solo-pruebas       solo prueba los endpoints sin poblar")
            return 2

    running = client.balance()
    created = errors = low_seen = mismatches = 0
    print(f"Creando {len(plan)} transacciones en orden cronológico...")
    for i, ev in enumerate(plan, 1):
        body = {"descripcion": ev["descripcion"], "monto": ev["monto"], "tipo": ev["tipo"],
                "categoria": ev["categoria"], "fecha": iso(ev["fecha"])}
        r = client.call("POST", "/transacciones", body)
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
    print(f"  Errores: {errors}")
    print(f"  Veces con saldoBajo=true: {low_seen} (discrepancias con lo esperado: {mismatches})")
    print(f"  Saldo final (API): {fmt(final_balance)} | esperado: {fmt(running)}")
    print()
    print(f"  {'Categoría':<18}{'Ingresos':>12}{'Egresos':>12}{'Neto':>12}")
    for row in client.summary():
        ing, egr = dec(row["totalIngresos"]), dec(row["totalEgresos"])
        print(f"  {row['categoria']:<18}{fmt(ing):>12}{fmt(egr):>12}{fmt(ing - egr):>12}")
    bad = errors or mismatches or final_balance != running
    return 1 if bad else 0


# ---------------------------------------------------------------------- pruebas

class Tester:
    def __init__(self, client):
        self.client = client
        self.passed = self.failed = self.skipped = 0
        self.created_ids = []

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

    def cleanup(self):
        for tx_id in self.created_ids:
            r = self.client.call("DELETE", f"/transacciones/{tx_id}")
            if r.status not in (204, 404):
                print(f"  Aviso: no se pudo borrar {tx_id} (HTTP {r.status})")
        self.created_ids.clear()


def run_tests(client):
    t = Tester(client)
    base_ids = sorted(x["id"] for x in client.list_all())
    base_balance = client.balance()
    print(f"Estado inicial: {len(base_ids)} transacciones, saldo {fmt(base_balance)}\n")

    sections = [test_create, test_validation, test_low_balance, test_get_by_id, test_filters,
                test_balance_and_summary, test_update, test_delete]
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


# ------------------------------------------------------------------------- main

def parse_args():
    p = argparse.ArgumentParser(description="Puebla y prueba la API de GestorGastos.")
    p.add_argument("--url", default=DEFAULT_URL, help=f"URL base de la API (por defecto {DEFAULT_URL})")
    mode = p.add_mutually_exclusive_group()
    mode.add_argument("--solo-datos", action="store_true", help="solo poblar datos de ejemplo")
    mode.add_argument("--solo-pruebas", action="store_true", help="solo probar los endpoints")
    safety = p.add_mutually_exclusive_group()
    safety.add_argument("--agregar", action="store_true", help="añade los datos aunque la API ya tenga transacciones")
    safety.add_argument("--limpiar", action="store_true", help="borra TODAS las transacciones antes de poblar")
    p.add_argument("--si", action="store_true", help="confirma --limpiar sin preguntar")
    p.add_argument("--semilla", type=int, default=2026, help="semilla del generador aleatorio (por defecto 2026)")
    p.add_argument("--dry-run", action="store_true", help="muestra el plan de datos sin escribir en la API")
    return p.parse_args()


def main():
    args = parse_args()
    if args.dry_run:
        if args.solo_pruebas:
            print("--dry-run solo aplica a la población de datos; no hay nada que mostrar con --solo-pruebas.")
            return 0
        print_plan(build_plan(args.semilla))
        return 0

    client = Client(args.url)
    try:
        client.balance()
    except (ApiUnavailable, RuntimeError) as e:
        print(f"No se pudo contactar con la API en {args.url}: {e}")
        print("Arráncala con:  dotnet run --project GestorGastos.Api --launch-profile http")
        return 3

    try:
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
