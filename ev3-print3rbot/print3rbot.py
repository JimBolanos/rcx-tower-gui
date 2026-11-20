#!/usr/bin/env pybricks-micropython
"""
EV3 Print3rbot (Christophe Avenel, 2015) - programa para Pybricks.

El robot es un brazo paralelo de dos brazos (five-bar / SCARA paralelo):
  * Motor grande IZQUIERDO (puerto C) y DERECHO (puerto A): cada uno mueve un tornillo
    sinfin que gira un engranaje de 40 dientes = el "hombro" de cada brazo (40:1).
  * Cada brazo tiene un "brazo" (hombro -> codo) y un "antebrazo" (codo -> lapiz).
    Los dos antebrazos se unen en el lapiz.
  * Motor mediano (puerto B): sube y baja el lapiz.
  * Sensores de toque (puertos 2 y 3): home de cada brazo.

Lee el dibujo de dibujo.py (generado por el convertidor): WIDTH, HEIGHT y STROKES
(lista de trazos, cada uno una lista de puntos (x, y) en mm, y hacia abajo como en la imagen).
El dibujo se escala y centra solo dentro del AREA de dibujo.

Coordenadas del robot (vistas desde arriba, mirando hacia donde apuntan los brazos):
  origen = punto medio entre los dos hombros, x = hacia la derecha, y = hacia adelante.
  Angulo de un brazo: 0 = apunta a la derecha, 90 = apunta al frente, 180 = a la izquierda.

ANTES DE DIBUJAR: mide D, L1 y L2 y sigue la calibracion del README (CALIBRAR = 1 y 2).
"""

from pybricks.hubs import EV3Brick
from pybricks.ev3devices import Motor, TouchSensor
from pybricks.parameters import Port, Stop, Direction, Button
from pybricks.tools import wait, StopWatch
from math import sqrt, atan2, acos, cos, sin, pi

# ==========================================================================
#  CONFIGURACION
# ==========================================================================

# 0 = dibujar dibujo.py
# 1 = home + poner los dos brazos apuntando al frente (para ajustar HOME_ANGLE_*)
# 2 = dibujar el borde del AREA y una cruz (para comprobar medidas)
CALIBRAR = 1

# --- Puertos (segun las instrucciones) ---
PORT_LEFT = Port.C          # motor grande izquierdo
PORT_RIGHT = Port.A         # motor grande derecho
PORT_PEN = Port.B           # motor mediano (lapiz)
PORT_TOUCH_LEFT = Port.S2   # sensor de toque izquierdo
PORT_TOUCH_RIGHT = Port.S3  # sensor de toque derecho

# --- Geometria en mm, de centro de eje a centro de eje (MIDELAS!) ---
# Truco: en una viga LEGO hay 8 mm entre agujeros. Cuenta agujeros y multiplica por 8.
D = 40.0     # entre los dos ejes verticales de los engranajes de 40 dientes (hombros)
L1 = 80.0    # eje del hombro -> eje del codo (bujes amarillos)
L2 = 100.0   # eje del codo -> centro del lapiz
GEAR = 40    # tornillo sinfin -> engranaje de 40 dientes

# --- Sentido de giro ---
# Debe quedar asi: angulo positivo del motor = el brazo gira ANTIHORARIO visto desde arriba.
# Si en CALIBRAR = 1 un brazo gira al reves, cambia su direccion.
DIR_LEFT = Direction.CLOCKWISE
DIR_RIGHT = Direction.CLOCKWISE

# --- Home con los sensores de toque ---
# Hacia donde gira cada brazo para tocar su sensor: +1 = antihorario, -1 = horario.
HOME_DIR_LEFT = 1
HOME_DIR_RIGHT = -1
# Angulo del brazo (0 = derecha, 90 = frente, 180 = izquierda) en el momento en que toca
# su sensor. Se ajusta con CALIBRAR = 1.
HOME_ANGLE_LEFT = 150.0
HOME_ANGLE_RIGHT = 30.0
HOME_SPEED = 300            # grados/s del motor durante el home
HOME_TIMEOUT_MS = 25000

# --- Area de dibujo (mm) ---
AREA_W = 60.0               # ancho
AREA_H = 45.0               # alto
AREA_CY = 120.0             # distancia del centro del area a la linea de los hombros
AJUSTAR = True              # True = escala el dibujo para que llene el area

# --- Velocidades (mm/s en la punta del lapiz) ---
SPEED_DRAW = 12.0
SPEED_TRAVEL = 25.0
SPEED_JOINT = 500           # grados/s del motor para movimientos sin dibujar

# --- Lapiz (motor mediano) ---
PEN_UP_ANGLE = -90          # grados de motor desde "lapiz tocando" hasta "lapiz arriba"
PEN_SPEED = 300
PEN_JOG_SPEED = 150
PEN_DELAY_MS = 150

# --- Seguridad ---
MIN_PEN_ANGLE = 25          # angulo minimo entre antebrazos en el lapiz (grados)
MIN_ELBOW_ANGLE = 10        # evita que brazo y antebrazo queden casi rectos

# ==========================================================================

ev3 = EV3Brick()
ml = Motor(PORT_LEFT, DIR_LEFT)
mr = Motor(PORT_RIGHT, DIR_RIGHT)
mpen = Motor(PORT_PEN)
tl = TouchSensor(PORT_TOUCH_LEFT)
tr = TouchSensor(PORT_TOUCH_RIGHT)

DEG = 180 / pi
pen_is_up = False
cur = None                  # posicion actual del lapiz (x, y) en mm


class Error(Exception):
    pass


def say(*lines):
    print(*lines)
    try:
        ev3.screen.clear()
        for ln in lines:
            ev3.screen.print(ln)
    except Exception:
        pass


def beep(f=880, ms=150):
    try:
        ev3.speaker.beep(f, ms)
    except Exception:
        pass


def motor_done(m):
    try:
        return m.done()
    except AttributeError:
        return m.control.done()


def motor_stalled(m):
    try:
        return m.stalled()
    except AttributeError:
        return m.control.stalled()


def wait_center():
    while Button.CENTER not in ev3.buttons.pressed():
        wait(20)
    while Button.CENTER in ev3.buttons.pressed():
        wait(20)


# ------------------------------------------------------------ cinematica --

def ik(x, y):
    """(x, y) del lapiz -> (angulo brazo izq, angulo brazo der) en grados, o None."""
    # brazo izquierdo: hombro en (-D/2, 0), codo hacia afuera (a la izquierda)
    dx = x + D / 2
    d = sqrt(dx * dx + y * y)
    if d < 1e-6:
        return None
    c = (L1 * L1 + d * d - L2 * L2) / (2 * L1 * d)
    if c <= -1 or c >= 1:
        return None
    al = acos(c)
    a_left = atan2(y, dx) + al
    # brazo derecho: hombro en (D/2, 0), codo hacia afuera (a la derecha)
    dx2 = x - D / 2
    d2 = sqrt(dx2 * dx2 + y * y)
    if d2 < 1e-6:
        return None
    c2 = (L1 * L1 + d2 * d2 - L2 * L2) / (2 * L1 * d2)
    if c2 <= -1 or c2 >= 1:
        return None
    ar = acos(c2)
    a_right = atan2(y, dx2) - ar
    if al * DEG < MIN_ELBOW_ANGLE or ar * DEG < MIN_ELBOW_ANGLE:
        return None
    # angulo entre antebrazos en el lapiz
    e1x, e1y = -D / 2 + L1 * cos(a_left), L1 * sin(a_left)
    e2x, e2y = D / 2 + L1 * cos(a_right), L1 * sin(a_right)
    v1x, v1y, v2x, v2y = e1x - x, e1y - y, e2x - x, e2y - y
    cp = (v1x * v2x + v1y * v2y) / (L2 * L2)
    cp = max(-1.0, min(1.0, cp))
    pen_ang = acos(cp) * DEG
    if pen_ang < MIN_PEN_ANGLE or pen_ang > 180 - MIN_PEN_ANGLE:
        return None
    return a_left * DEG, a_right * DEG


def motor_targets(angles):
    a_left, a_right = angles
    return (a_left - HOME_ANGLE_LEFT) * GEAR, (a_right - HOME_ANGLE_RIGHT) * GEAR


def track_xy(x, y):
    t = ik(x, y)
    if t is None:
        raise Error("Fuera de alcance: (%.1f, %.1f)" % (x, y))
    l, r = motor_targets(t)
    ml.track_target(l)
    mr.track_target(r)


# ------------------------------------------------------------ movimiento --

def joint_move(a_left, a_right):
    """Mueve los dos brazos a la vez (por angulos) para que lleguen juntos."""
    l, r = motor_targets((a_left, a_right))
    dl = abs(l - ml.angle())
    dr = abs(r - mr.angle())
    dm = max(dl, dr, 1)
    ml.run_target(max(SPEED_JOINT * dl / dm, 20), l, Stop.HOLD, False)
    mr.run_target(max(SPEED_JOINT * dr / dm, 20), r, Stop.HOLD, False)
    while not (motor_done(ml) and motor_done(mr)):
        wait(5)


def line_to(x, y, speed):
    """Linea recta del punto actual a (x, y), a velocidad constante en mm/s."""
    global cur
    x0, y0 = cur
    length = sqrt((x - x0) ** 2 + (y - y0) ** 2)
    if length < 0.05:
        return
    total_ms = length / speed * 1000
    timer = StopWatch()
    while True:
        f = timer.time() / total_ms
        if f >= 1:
            break
        track_xy(x0 + (x - x0) * f, y0 + (y - y0) * f)
        wait(10)
    track_xy(x, y)
    cur = (x, y)


def pen_up():
    global pen_is_up
    if not pen_is_up:
        mpen.run_target(PEN_SPEED, PEN_UP_ANGLE, Stop.HOLD, True)
        wait(PEN_DELAY_MS)
        pen_is_up = True


def pen_down():
    global pen_is_up
    if pen_is_up:
        wait(80)  # que los brazos terminen de llegar
        mpen.run_target(PEN_SPEED, 0, Stop.HOLD, True)
        wait(PEN_DELAY_MS)
        pen_is_up = False


# ------------------------------------------------------------ preparacion --

def pen_setup():
    """El usuario ajusta el lapiz con los botones: esa posicion = lapiz tocando el papel."""
    global pen_is_up
    say("AJUSTE DEL LAPIZ", "", "Arriba/Abajo: mover", "Centro: listo", "(lapiz tocando", " el papel)")
    up_sign = -1 if PEN_UP_ANGLE < 0 else 1
    while True:
        b = ev3.buttons.pressed()
        if Button.CENTER in b:
            break
        if Button.UP in b:
            mpen.run(up_sign * PEN_JOG_SPEED)
        elif Button.DOWN in b:
            mpen.run(-up_sign * PEN_JOG_SPEED)
        else:
            mpen.hold()
        wait(20)
    mpen.hold()
    while Button.CENTER in ev3.buttons.pressed():
        wait(20)
    mpen.reset_angle(0)
    pen_is_up = False


def home():
    """Gira los dos brazos a la vez hasta que cada uno toca su sensor."""
    say("Home...")
    sl = HOME_SPEED * HOME_DIR_LEFT
    sr = HOME_SPEED * HOME_DIR_RIGHT
    left_ok = tl.pressed()
    right_ok = tr.pressed()
    if not left_ok:
        ml.run(sl)
    if not right_ok:
        mr.run(sr)
    timer = StopWatch()
    wait(300)
    while not (left_ok and right_ok):
        if not left_ok and tl.pressed():
            ml.hold()
            left_ok = True
        if not right_ok and tr.pressed():
            mr.hold()
            right_ok = True
        if timer.time() > HOME_TIMEOUT_MS or (not left_ok and motor_stalled(ml)) or (not right_ok and motor_stalled(mr)):
            ml.hold()
            mr.hold()
            raise Error("Home fallo: revisa HOME_DIR_%s" % ("LEFT" if not left_ok else "RIGHT"))
        wait(5)
    ml.reset_angle(0)
    mr.reset_angle(0)


def park():
    """Desde el home, lleva el lapiz al centro del area."""
    global cur
    t = ik(0, AREA_CY)
    if t is None:
        raise Error("El centro del area no se alcanza: revisa D, L1, L2, AREA_CY")
    joint_move(t[0], t[1])
    cur = (0.0, AREA_CY)


# ------------------------------------------------------------ dibujo --------

def fit(strokes, width, height):
    """Convierte coordenadas del dibujo (mm, y hacia abajo) a coordenadas del robot."""
    s = min(AREA_W / width, AREA_H / height) if AJUSTAR else 1.0
    out = []
    for st in strokes:
        out.append([((x - width / 2) * s, AREA_CY + (height / 2 - y) * s) for (x, y) in st])
    return out, s


def check(strokes):
    for st in strokes:
        for (x, y) in st:
            if ik(x, y) is None:
                raise Error("Punto fuera de alcance (%.0f,%.0f). Achica AREA_W/AREA_H o ajusta AREA_CY" % (x, y))


def draw(strokes):
    timer = StopWatch()
    total = len(strokes)
    for i, st in enumerate(strokes):
        if i % 5 == 0:
            say("Trazo %d/%d" % (i + 1, total), "%d s" % (timer.time() // 1000), "", "Centro = pausa")
        pen_up()
        line_to(st[0][0], st[0][1], SPEED_TRAVEL)
        pen_down()
        for (x, y) in st[1:]:
            line_to(x, y, SPEED_DRAW)
        if Button.CENTER in ev3.buttons.pressed():
            pen_up()
            say("PAUSA", "Centro = seguir")
            wait(500)
            wait_center()
    pen_up()
    return timer.time() // 1000


def area_strokes():
    w, h, cy = AREA_W / 2, AREA_H / 2, AREA_CY
    return [
        [(-w, cy - h), (w, cy - h), (w, cy + h), (-w, cy + h), (-w, cy - h)],
        [(-10, cy), (10, cy)],
        [(0, cy - 10), (0, cy + 10)],
    ]


def main():
    if CALIBRAR == 0:
        from dibujo import STROKES, WIDTH, HEIGHT
        strokes, s = fit(STROKES, WIDTH, HEIGHT)
        title = "Dibujo x%.2f" % s
    else:
        strokes = area_strokes()
        title = "Calibracion %d" % CALIBRAR
    if CALIBRAR != 1:
        check(strokes)

    say(title, "%d trazos" % len(strokes), "", "Centro = empezar")
    wait_center()
    pen_setup()
    pen_up()
    home()

    if CALIBRAR == 1:
        joint_move(90, 90)
        say("Los dos brazos deben", "apuntar al frente,", "paralelos.", "", "Centro = terminar")
        beep()
        wait_center()
        return

    park()
    secs = draw(strokes)
    line_to(0, AREA_CY, SPEED_TRAVEL)
    say("Listo!", "%d s" % secs)
    beep()
    wait(3000)


try:
    main()
except Error as e:
    ml.hold()
    mr.hold()
    say("ERROR", str(e))
    beep(220, 600)
    wait(10000)
