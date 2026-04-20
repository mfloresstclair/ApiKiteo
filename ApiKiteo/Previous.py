​# ============================================================
# KiteoAdmin + KiteoApp API (ordenado segun Swagger v3.0)
# ============================================================

from flask import Flask, request, jsonify, abort
from flask_cors import CORS
from waitress import serve

import os
import base64
import urllib.parse
import re
import json
import socket

from Crypto.Cipher import AES
from Crypto.Util.Padding import unpad

from sqlalchemy import create_engine, text
from werkzeug.exceptions import HTTPException

from ldap3 import Server, Connection, ALL
from ldap3.core.exceptions import LDAPException


# ============================================================
# App
# ============================================================
app = Flask(__name__)
CORS(app)


# ============================================================
# Environment variables (DB encryption)
# ============================================================
kN = "Thragg"   # AES Key
VN = "DvT"    # Encrypted connection string

K = os.environ.get(kN)
V = os.environ.get(VN)
if not K or not V:
    raise RuntimeError("Faltan    variables de entorno")


# ============================================================
# Crypto
# ============================================================
def DecryptString(cipherText, key):
    try:
        raw = base64.b64decode(cipherText)
        iv = raw[:16]
        cipher = raw[16:]
        aes = AES.new(key.encode(), AES.MODE_CBC, iv)
        decrypted = unpad(aes.decrypt(cipher), AES.block_size)
        return decrypted.decode("utf-8")
    except Exception as e:
        raise RuntimeError(f"Error desencriptando conexion: {e}")


# ============================================================
# DB Engine
# ============================================================
conn_raw = DecryptString(V, K)

conn_str = re.sub(r"(?i)\bdata\s*source\s*=", "SERVER=", conn_raw)
conn_str = re.sub(r"(?i)\buser\s*id\s*=", "UID=", conn_str)
conn_str = re.sub(r"(?i)\bpassword\s*=", "PWD=", conn_str)

if "DRIVER=" not in conn_str.upper():
    conn_str = "DRIVER={ODBC Driver 18 for SQL Server};" + conn_str

conn_str = re.sub(r";\s+", ";", conn_str.strip())
if not conn_str.endswith(";"):
    conn_str += ";"

if "DATABASE=" not in conn_str.upper():
    conn_str += "DATABASE=BOS;"

if "TRUSTSERVERCERTIFICATE" not in conn_str.upper():
    conn_str += "TrustServerCertificate=yes;"

safe = re.sub(r"(?i)PWD=[^;]*", "PWD=***", conn_str)
print("ODBC conn_str FINAL:", safe)

quoted = urllib.parse.quote_plus(conn_str)
engine = create_engine(
    f"mssql+pyodbc:///?odbc_connect={quoted}",
    pool_pre_ping=True
)


# ============================================================
# Active Directory config (Windows environment)
# ============================================================
def ad_config():
    fqdn = socket.getfqdn()
    domain_parts = fqdn.split(".", 1)
    domain_dns = domain_parts[1] if len(domain_parts) > 1 else "stclairtech.local"

    return {
        "host": domain_dns,                     # DC via DNS
        "port": 636,
        "use_ssl": True,
        "domain": domain_dns.split(".")[0].upper(),   # STCLAIRTECH
        "base_dn": "DC=stclairtech,DC=local",
        "search_base": "DC=stclairtech,DC=local",
        "bind_dn": "CN=Svc_LDAP,CN=Users,DC=stclairtech,DC=local",
        "bind_pwd": "********"  # TODO: mover a un lugar seguro
    }


# ============================================================
# Stored Procedures (EXISTENTES KiteoApp)
# ============================================================
SP_GET_SEMANAS          = "Kit_vin_wk"
SP_EMP_CHECK            = "Kit_vin_Emp"
SP_SEMANA_LOC           = "Kit_vin_Wk_Loc"
SP_SEMANA_GRP_STATUS    = "Kit_vin_Wk_Grp_Status"
SP_WK_FALTANTES_GRP     = "Kit_vin_wk_faltantes_grupo"
SP_SEMANA_VIN_STATUS    = "Kit_vin_Wk_Vin_Status"
SP_WK_VIN_ENTREGADOS    = "Kit_vin_entregado_final"
SP_ESCANEAR             = "Kit_vin_Scan"
SP_SCAN_AJUSTE          = "Kit_vin_Scan_Ajuste"      # (tu SP ajustado debe aceptar @jsonVines)
SP_USER_ACCESS          = "Kit_vin_User_Access"
SP_VIN_TO_ADJUST        = "Kit_vin_to_adjust"
SP_VIN_WK_PEND          = "Kit_vin_wk_pend"

# ============================================================
# Stored Procedures (NUEVOS KiteoAdmin)
# ============================================================
SP_WK_APPROVE           = "Kit_vin_wk_approve"   # Swagger v3.0
# SP_WK_CREATE          = "sp_CrearSemanaEnDB" # TODO
# SP_VERIF_CARGA        = "sp_VerificarCargaSemana" # TODO
# etc...


# ============================================================
# Helpers
# ============================================================
def error_response(status, mensaje, codigo):
    return jsonify({
        "exito": False,
        "mensaje": mensaje,
        "codigo": codigo
    }), status


def ad_authenticate(username, password):
    cfg = ad_config()
    if not cfg["host"] or not cfg["domain"]:
        return False, "Configuracion AD incompleta."

    user_upn = f"{cfg['domain']}\\{username}"
    server = Server(cfg["host"], port=cfg["port"], use_ssl=cfg["use_ssl"], get_info=ALL)

    try:
        conn = Connection(server, user=user_upn, password=password, auto_bind=True)
        conn.unbind()
        return True, "OK"
    except LDAPException:
        return False, "Credenciales invalidas o no autorizado."


def kit_vin_User_Access(username: str):
    sql = f"""
        EXEC {SP_USER_ACCESS}
            @username = :username
    """
    with engine.begin() as conn:
        rows = conn.execute(text(sql), {"username": username}).mappings().all()

    if not rows:
        return None

    lp = False
    fa = False

    for rr in rows:
        r = dict(rr)

        acc = r.get("access") or r.get("ACCESS") or r.get("Access")
        if isinstance(acc, str):
            acc_norm = acc.strip().lower()
            if acc_norm == "lpaccess":
                lp = True
            elif acc_norm == "faaccess":
                fa = True

        if r.get("LPaccess") or r.get("lpaccess"):
            lp = True
        if r.get("FAaccess") or r.get("faaccess"):
            fa = True

    return {"LPaccess": lp, "FAaccess": fa}


# ============================================================
# ============================================================
# EXISTENTES — KiteoApp v2.7 (no modificar funcionalidad)
# ============================================================
# ============================================================

# ------------------------------------------------------------
# Auth (Swagger: /auth/login)
# ------------------------------------------------------------
@app.route("/auth/login", methods=["POST"])
def auth_login():
    data = request.get_json() or {}
    username = (data.get("username") or "").strip()
    password = (data.get("password") or "").strip()

    if not username or not password:
        return error_response(400, "Los campos 'username' y 'password' son requeridos.", "AUTH_400")

    try:
        ok, msg = ad_authenticate(username, password)
        if not ok:
            return error_response(401, msg, "AUTH_401")

        access = kit_vin_User_Access(username)
        if (not access) or (not access.get("LPaccess") and not access.get("FAaccess")):
            return error_response(401, "Usuario sin acceso a la aplicacion.", "AUTH_NO_ACCESS")

        resp = {"ok": True, "username": username}
        if access.get("LPaccess"):
            resp["access"] = "LPaccess"
        elif access.get("FAaccess"):
            resp["access"] = "FAaccess"

        return jsonify(resp), 200

    except Exception as e:
        print("ERROR /auth/login:", e)
        return error_response(500, "Error interno. Contacta a soporte.", "AUTH_500")


# ------------------------------------------------------------
# Semanas (Swagger: /semanas)
# CAMBIO v3.0: incluir estatus si el SP lo devuelve.
# ------------------------------------------------------------
@app.route("/semanas", methods=["GET"])
def get_semanas():
    cliente = (request.args.get("cliente") or "").strip()
    tipo = (request.args.get("tipo") or "").strip()

    if not cliente or not tipo:
        return ("Los parametros 'cliente' y 'tipo' son requeridos.", 400)

    try:
        sql = f"""
            EXEC {SP_GET_SEMANAS}
                @cliente = :cliente,
                @tipo    = :tipo
        """

        with engine.begin() as conn:
            rows = conn.execute(text(sql), {"cliente": cliente, "tipo": tipo}).mappings().all()

        if not rows:
            return ("No hay semanas cargadas para este cliente y tipo seleccionado.", 401)

        # Respuesta compatible:
        # - Siempre "clave"
        # - Incluye "estatus" solo si viene en la salida del SP
        response = []
        for r in rows:
            rr = dict(r)
            item = {"clave": rr.get("clave") or rr.get("wkname")}
            if rr.get("estatus") is not None:
                item["estatus"] = rr.get("estatus")
            response.append(item)

        return jsonify(response), 200

    except Exception as e:
        print("ERROR /semanas:", e)
        return ("Error interno. Contacta a soporte.", 500)


# ------------------------------------------------------------
# Semanas (extra existente): /semanas_pendientes
# (No esta en tu Swagger v3.0, pero se deja existente)
# ------------------------------------------------------------
@app.route("/semanas_pendientes", methods=["GET"])
def get_semanas_pendientes():
    try:
        sql = f"EXEC {SP_VIN_WK_PEND}"
        with engine.begin() as conn:
            rows = conn.execute(text(sql)).mappings().all()

        if not rows:
            return jsonify([]), 200

        response = [{"wkname": r.get("wkname")} for r in rows if r.get("wkname")]
        return jsonify(response), 200

    except Exception as e:
        print("ERROR /semanas_pendientes:", e)
        return error_response(500, "Error interno. Contacta a soporte.", "KITEO_500")


# ------------------------------------------------------------
# Empleados (Swagger: /empleado)
# ------------------------------------------------------------
@app.route("/empleado", methods=["GET"])
def get_empleado():
    empleado = (request.args.get("empleado") or "").strip()

    if not empleado:
        return error_response(400, "El parametro 'empleado' es requerido.", "KITEO_400")

    try:
        sql = f"""
            EXEC {SP_EMP_CHECK}
                @empleado = :empleado
        """

        with engine.begin() as conn:
            rows = conn.execute(text(sql), {"empleado": empleado}).mappings().all()

        if not rows:
            abort(404, description="Empleado no encontrado.")

        return jsonify({"nombre": rows[0].get("nombre")}), 200

    except HTTPException:
        raise
    except Exception as e:
        print("ERROR /empleado:", e)
        return error_response(500, "Error interno. Contacta a soporte.", "KITEO_500")


# ------------------------------------------------------------
# VINs (Swagger: /semana_loc)
# ------------------------------------------------------------
@app.route("/semana_loc", methods=["GET"])
def get_semana_loc():
    wkname = (request.args.get("wkname") or "").strip()

    if not wkname:
        return error_response(400, "El parametro 'wkname' es requerido.", "KITEO_400")

    try:
        sql = f"""
            EXEC {SP_SEMANA_LOC}
                @wkname = :wkname
        """

        with engine.begin() as conn:
            rows = conn.execute(text(sql), {"wkname": wkname}).mappings().all()

        response = []
        for r in rows:
            rr = dict(r)
            # v3.0: opcionales (grupo/item/descripcion) sin romper
            response.append({
                "vin": rr.get("vin"),
                "locacion": rr.get("locacion"),
                "grupo": rr.get("grupo"),
                "item": rr.get("item"),
                "descripcion": rr.get("descripcion")
            })

        return jsonify({
            "ok": True,
            "wkname": wkname,
            "total": len(response),
            "resultados": response
        }), 200

    except Exception as e:
        print("ERROR /semana_loc:", e)
        return error_response(500, "Error interno. Contacta a soporte.", "KITEO_500")


# ------------------------------------------------------------
# VINs (Swagger: /semana_grp_status)
# ------------------------------------------------------------
@app.route("/semana_grp_status", methods=["GET"])
def get_semana_grp_status():
    wkname = (request.args.get("wkname") or "").strip()

    if not wkname:
        return error_response(400, "El parametro 'wkname' es requerido.", "KITEO_400")

    try:
        sql = f"""
            EXEC {SP_SEMANA_GRP_STATUS}
                @wkname = :wkname
        """

        with engine.begin() as conn:
            rows = conn.execute(text(sql), {"wkname": wkname}).mappings().all()

        resultados = [{
            "Grupo": r["Grupo"],
            "vines": r["vines"],
            "Porcentaje": float(r["Porcentaje"])
        } for r in rows]

        return jsonify({
            "ok": True,
            "wkname": wkname,
            "total": len(resultados),
            "resultados": resultados
        }), 200

    except Exception as e:
        print("ERROR /semana_grp_status:", e)
        return error_response(500, "Error interno. Contacta a soporte.", "KITEO_500")

# ============================================================
# POST /semana_grp_faltantes
# ============================================================
@app.route("/semana_grp_faltantes", methods=["POST"])
def get_semana_grp_faltantes():
    data = request.get_json() or {}

    wkname = (data.get("wkname") or "").strip()
    det    = (data.get("det") or "1").strip()
    grupos = data.get("grupos")

    if not wkname:
        return error_response(
            400,
            "El campo 'wkname' es requerido.",
            "KITEO_400"
        )

    if not grupos or not isinstance(grupos, list):
        return error_response(
            400,
            "El campo 'grupos' debe ser una lista.",
            "KITEO_400"
        )

    try:
        # 🔹 Reconstruimos el JSON exactamente como lo espera SQL
        json_grupos = {
            "grupos": grupos
        }

        sql = f"""
            EXEC {SP_WK_FALTANTES_GRP}
                @wkname     = :wkname,
                @jsonGrupos = :jsonGrupos,
                @det        = :det
        """

        with engine.begin() as conn:
            rows = conn.execute(
                text(sql),
                {
                    "wkname": wkname,
                    "jsonGrupos": json.dumps(json_grupos),
                    "det": det
                }
            ).mappings().all()

        # 🔹 Respuesta genérica (funciona para resumen y detalle)
        resultados = [
            {k: r[k] for k in r.keys()}
            for r in rows
        ]

        return jsonify({
            "ok": True,
            "wkname": wkname,
            "det": det,
            "total": len(resultados),
            "resultados": resultados
        }), 200

    except Exception as e:
        print("ERROR /semana_grp_faltantes:", e)
        return error_response(
            500,
            "Error interno. Contacta a soporte.",
            "KITEO_500"
        )

# ------------------------------------------------------------
# VINs (Swagger: /semana_vin_status)
# ------------------------------------------------------------
@app.route("/semana_vin_status", methods=["GET"])
def get_semana_vin_status():
    wkname = (request.args.get("wkname") or "").strip()
    cliente = (request.args.get("cliente") or "").strip()
    tipo = (request.args.get("tipo") or "").strip()

    if not wkname or not cliente or not tipo:
        return error_response(400, "Faltan parametros requeridos (wkname, cliente, tipo).", "KITEO_400")

    try:
        sql = f"""
            EXEC {SP_SEMANA_VIN_STATUS}
                @wkname = :wkname,
                @cliente = :cliente,
                @tipo = :tipo
        """

        with engine.begin() as conn:
            rows = conn.execute(text(sql), {"wkname": wkname, "cliente": cliente, "tipo": tipo}).mappings().all()

        resultados = [{
            "Locacion": r["Locacion"],
            "Vin": r["Vin"],
            "Porcentaje": float(r["Porcentaje"])
        } for r in rows]

        return jsonify({
            "ok": True,
            "wkname": wkname,
            "total": len(resultados),
            "resultados": resultados
        }), 200

    except Exception as e:
        print("ERROR /semana_vin_status:", e)
        return error_response(500, "Error interno. Contacta a soporte.", "KITEO_500")


# ------------------------------------------------------------
# Escaneo (Swagger: /vin_to_adjust)
# ------------------------------------------------------------
@app.route("/vin_to_adjust", methods=["POST"])
def vin_to_adjust():
    data = request.get_json() or {}

    wkname = (data.get("wkname") or "").strip()
    item = (data.get("item") or "").strip()
    empleado = (data.get("empleado") or "").strip()

    if not wkname or not item or not empleado:
        return error_response(400, "Los campos 'wkname', 'item' y 'empleado' son requeridos.", "KITEO_400")

    try:
        sql = f"""
            EXEC {SP_VIN_TO_ADJUST}
                @wkname   = :wkname,
                @item     = :item,
                @empleado = :empleado
        """

        with engine.begin() as conn:
            rows = conn.execute(text(sql), {"wkname": wkname, "item": item, "empleado": empleado}).mappings().all()

        vines = []
        for r in rows:
            rr = dict(r)
            if rr.get("vin"):
                vines.append({
                    "vin": rr.get("vin"),
                    "loc": rr.get("Loc") or rr.get("Locacion") or rr.get("loc") or rr.get("locacion"),
                    "item": rr.get("item") or item,
                    "grupo": rr.get("Grupo") or rr.get("grupo")
                })

        return jsonify({
            "ok": True,
            "wkname": wkname,
            "item": item,
            "total": len(vines),
            "vines": vines
        }), 200

    except Exception as e:
        print("ERROR /vin_to_adjust:", e)
        return error_response(500, "Error interno. Contacta a soporte.", "KITEO_500")


# ------------------------------------------------------------
# Escaneo (Swagger: /escanear_ajuste) - lista de VINs
# ------------------------------------------------------------
@app.route("/escanear_ajuste", methods=["POST"])
def escanear_ajuste():
    data = request.get_json() or {}

    required = ["wkname", "item", "empleado", "vines"]
    missing = [k for k in required if k not in data]
    if missing:
        return jsonify({"ok": False, "mensaje": f"Faltan campos requeridos: {', '.join(missing)}"}), 400

    wkname = (data.get("wkname") or "").strip()
    item = (data.get("item") or "").strip()
    empleado = (data.get("empleado") or "").strip()
    vines_in = data.get("vines")

    if not wkname or not item or not empleado:
        return jsonify({"ok": False, "mensaje": "Los campos wkname/item/empleado no pueden ir vacios."}), 400

    if not isinstance(vines_in, list) or len(vines_in) == 0:
        return jsonify({"ok": False, "mensaje": "El campo vines debe ser una lista con al menos 1 VIN."}), 400

    json_vines = {"vines": vines_in}

    try:
        sql = f"""
            EXEC {SP_SCAN_AJUSTE}
                @wkname    = :wkname,
                @item      = :item,
                @jsonVines = :jsonVines,
                @empleado  = :empleado
        """

        with engine.begin() as conn:
            rows = conn.execute(
                text(sql),
                {"wkname": wkname, "item": item, "jsonVines": json.dumps(json_vines), "empleado": empleado}
            ).mappings().all()

        evento = None
        vines_out = []

        for rr in rows:
            r = dict(rr)
            tipo = r.get("Tipo")

            if tipo == "EvtData" and evento is None:
                evento = {
                    "mensaje": r.get("mensaje"),
                    "actualizados": r.get("ajustados") or r.get("actualizados"),
                    "pendientes": r.get("disponibles_para_ajuste") or r.get("pendientes"),
                    "requested": r.get("solicitado") or r.get("requested"),
                    "total_item": r.get("total_item"),
                    "excedente": r.get("excedente"),
                    "faltante": r.get("faltante"),
                    "locaciones_ajustadas": r.get("locaciones_ajustadas")
                }
                continue

            vin = r.get("vin") or r.get("Vin")
            if vin:
                vines_out.append({
                    "vin": vin,
                    "loc": r.get("loc") or r.get("locacion") or r.get("Locacion"),
                    "grupo": r.get("grupo") or r.get("Grupo"),
                    "item": r.get("item") or item
                })

        return jsonify({
            "ok": True,
            "wkname": wkname,
            "item": item,
            "total": len(vines_out),
            "evento": evento,
            "vines": vines_out
        }), 200

    except Exception as e:
        print("ERROR /escanear_ajuste:", e)
        return jsonify({"ok": False, "mensaje": "Error interno. Contacta a soporte."}), 500


# ------------------------------------------------------------
# Escaneo (Swagger: /semana_vines_entrega)
# ------------------------------------------------------------
@app.route("/semana_vines_entrega", methods=["POST"])
def semana_vines_entrega():
    data = request.get_json() or {}

    wkname = (data.get("wkname") or "").strip()
    empleado = (data.get("empleado") or "").strip()
    vines = data.get("vines")
    comentario = (data.get("comentario") or "").strip()  # opcional
    supervisor = (data.get("supervisor") or "").strip()  # opcional


    if not wkname or not empleado:
        return error_response(400, "Los campos 'wkname' y 'empleado' son requeridos.", "KITEO_400")

    if not vines or not isinstance(vines, list):
        return error_response(400, "El campo 'vines' debe ser una lista.", "KITEO_400")

    try:
        json_vines = {"vines": vines}

        sql = f"""
            EXEC {SP_WK_VIN_ENTREGADOS}
                @wkname    = :wkname,
                @jsonVines = :jsonVines,
                @empleado  = :empleado,
                @comentario = :comentario,
                @supervisor = :supervisor
                    
        """

        with engine.begin() as conn:
            rows = conn.execute(
                text(sql),
                {"wkname": wkname, "jsonVines": json.dumps(json_vines), "empleado": empleado, "comentario": comentario, "supervisor": supervisor}
            ).mappings().all()

        resultados = [{k: r[k] for k in r.keys()} for r in rows]

        return jsonify({
            "ok": True,
            "wkname": wkname,
            "empleado": empleado,
            "total_actualizados": len(vines),
            "vines_actualizados": resultados
        }), 200

    except Exception as e:
        print("ERROR /semana_vines_entrega:", e)
        return error_response(500, "Error interno. Contacta a soporte.", "KITEO_500")


# ------------------------------------------------------------
# Escaneo (EXISTENTE KiteoApp): /escanear
# (No esta incluido en tu Swagger muestra, pero existe en API real)
# ------------------------------------------------------------
@app.route("/escanear", methods=["POST"])
def escanear():
    data = request.get_json() or {}
    required = ["wkname", "item", "cantidad", "empleado"]
    missing = [k for k in required if k not in data]
    if missing:
        return jsonify({"ok": False, "mensaje": f"Faltan campos requeridos: {', '.join(missing)}"}), 400

    wkname = data["wkname"]
    item = data["item"]
    cantidad = int(data["cantidad"])
    empleado = data["empleado"]

    try:
        sql = """
            EXEC Kit_vin_Scan
                @wkname   = :wkname,
                @item     = :item,
                @cantidad = :cantidad,
                @empleado = :empleado
        """

        with engine.begin() as conn:
            rows = conn.execute(text(sql), {"wkname": wkname, "item": item, "cantidad": cantidad, "empleado": empleado}).mappings().all()

        evento = None
        vines = []
        grupos_map = {}

        for r in rows:
            r = dict(r)

            if "mensaje" in r and evento is None:
                evento = {
                    "mensaje": r.get("mensaje"),
                    "actualizados": r.get("actualizados"),
                    "pendientes": r.get("pendientes"),
                    "requested": r.get("requested"),
                    "total_item": r.get("total_item"),
                    "excedente": r.get("excedente"),
                    "faltante": r.get("faltante"),
                }

            if r.get("vin"):
                vines.append({"vin": r.get("vin"), "loc": r.get("loc"), "grupo": r.get("grupo")})

            porcentaje = r.get("Porcentaje")
            grupo = r.get("grupo")
            if porcentaje is not None and grupo not in grupos_map:
                grupos_map[grupo] = {"grupo": grupo, "Porcentaje": str(porcentaje)}

        return jsonify({
            "ok": True,
            "wkname": wkname,
            "item": item,
            "total": len(vines),
            "evento": evento,
            "vines": vines,
            "grupos_progreso": list(grupos_map.values())
        }), 200

    except Exception as e:
        print("ERROR /escanear:", e)
        return jsonify({"ok": False, "mensaje": "Error interno. Contacta a soporte."}), 500


# ============================================================
# ============================================================
# NUEVOS — KiteoAdmin Dashboard v3.0 (se dejan TODO donde faltan)
# ============================================================
# ============================================================

# ------------------------------------------------------------
# Admin — Roles
# TODO: /api/roles (GET)
# TODO: /api/usuarios (GET/POST)
# TODO: /api/usuarios/{id} (PUT)
# ------------------------------------------------------------
# @app.route("/api/roles", methods=["GET"])
# def api_roles_get():
#     pass
#
# @app.route("/api/usuarios", methods=["GET", "POST"])
# def api_usuarios_get_post():
#     pass
#
# @app.route("/api/usuarios/<int:id>", methods=["PUT"])
# def api_usuarios_put(id):
#     pass


# ------------------------------------------------------------
# Admin — Semanas
# Implementado: /api/semanas/aprobar
# TODO: /api/semanas/crear
# ------------------------------------------------------------
@app.route("/api/semanas/aprobar", methods=["POST"])
def api_semanas_aprobar():
    data = request.get_json() or {}
    wkname = (data.get("wkname") or "").strip()
    aprobadoPor = (data.get("aprobadoPor") or "").strip()

    if not wkname or not aprobadoPor:
        return error_response(400, "Los campos 'wkname' y 'aprobadoPor' son requeridos.", "KITEO_400")

    try:
        # Opcion A: SP devuelve http_status/message/code (recomendado)
        sql = f"""
            EXEC {SP_WK_APPROVE}
                @wkname = :wkname,
                @aprobadoPor = :aprobadoPor
        """

        with engine.begin() as conn:
            rows = conn.execute(text(sql), {"wkname": wkname, "aprobadoPor": aprobadoPor}).mappings().all()

        # Si el SP devuelve un row con http_status:
        if rows and ("http_status" in rows[0] or "httpStatus" in rows[0]):
            r = dict(rows[0])
            status = int(r.get("http_status") or r.get("httpStatus") or 500)
            if status != 200:
                return error_response(status, r.get("message", "Error"), r.get("code", "ERROR"))
            return jsonify({"ok": True, "mensaje": "Semana aprobada"}), 200

        # Opcion B: SP no devuelve resultset -> asumimos exito si no truena
        # (Si quieres validar rowcount, el SP debe hacer UPDATE directo y no solo SELECT)
        return jsonify({"ok": True, "mensaje": "Semana aprobada"}), 200

    except Exception as e:
        print("ERROR /api/semanas/aprobar:", e)
        return error_response(500, "Error interno. Contacta a soporte.", "KITEO_500")


# @app.route("/api/semanas/crear", methods=["POST"])
# def api_semanas_crear():
#     # TODO: llamar sp_CrearSemanaEnDB
#     pass


# ------------------------------------------------------------
# Admin — MandarFinal
# TODO: /api/mandarfinal/verificar (GET)
# TODO: /api/mandarfinal (GET/POST/DELETE)
# TODO: /api/mandarfinal/catalogo (GET)
# ------------------------------------------------------------

# ------------------------------------------------------------
# Admin — Contenido
# TODO: /api/semanas/verificar_carga (GET)
# TODO: /api/semanas/contenido (GET)
# TODO: /api/semanas/contenido/bulk (POST)
# ------------------------------------------------------------


# ============================================================
# Run
# ============================================================
if __name__ == "__main__":
    serve(app, host="0.0.0.0", port=5000)