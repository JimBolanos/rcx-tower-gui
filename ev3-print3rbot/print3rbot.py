#!/usr/bin/env pybricks-micropython
"""
EV3 Print3rbot - plotter para Pybricks (EV3).

Funciona con:
  * Pybricks v2.0 para EV3 (imagen microSD + extension de VS Code "LEGO MINDSTORMS EV3").
  * El firmware Pybricks nuevo para EV3 (code.pybricks.com).

Lee el dibujo desde dibujo.py (generado por convertidor.html), que define:
  WIDTH, HEIGHT  -> tamano del dibujo en mm
  STROKES        -> lista de trazos; cada trazo es una lista de puntos (x, y) en mm

Coordenadas: (0, 0) = esquina donde el robot hace "home".
  X = movimiento del carro (motor X), Y = avance (motor Y).

ANTES DE DIBUJAR: pon CALIBRAR = True, dibuja el cuadrado de prueba, mide sus lados
y ajusta DEG_PER_MM_X / DEG_PER_MM_Y (ver README.md).
"""

from pybricks.hubs import EV3Brick
from pybricks.ev3devices import Motor, TouchSensor
from pybricks.parameters import Port, Stop, Direction, Button
from pybricks.tools import wait, StopWatch

# ==========================================================================
#  CONFIGURACION  (ajusta esto a tu robot)
# ==========================================================================

CALIBRAR = False          # True = dibuja un cuadrado de 50 mm en vez del dibujo

# --- Puertos ---
PORT_X = Port.A           # motor grande que mueve el carro (izquierda/derecha)
PORT_Y = Port.B           # motor grande que mueve el eje Y (avance/papel)
PORT_PEN = Port.C         # motor mediano que sube/baja el lapiz
PORT_TOUCH_X = Port.S1    # sensor de toque (fin de carrera X). None si no hay
PORT_TOUCH_Y = None       # segundo sensor de toque (fin de carrera Y). Ej: Port.S2

# --- Sentido de giro (cambia a COUNTERCLOCKWISE si un eje va al reves) ---
DIR_X = Direction.CLOCKWISE
DIR_Y = Direction.CLOCKWISE
DIR_PEN = Direction.CLOCKWISE

# --- Escala: grados de motor por milimetro (CALIBRAR!) ---
DEG_PER_MM_X = 12.0
DEG_PER_MM_Y = 12.0

# --- Area util (mm). Los puntos fuera se recortan ---
MAX_X_MM = 120
MAX_Y_MM = 150

# --- Desplazamiento del dibujo desde el home (mm) ---
OFFSET_X_MM = 5
OFFSET_Y_MM = 5
ESCALA = 1.0              # escala extra sobre el dibujo (1.0 = tal cual)

# --- Velocidades (grados/s) ---
SPEED_DRAW = 250          # dibujando (mas lento = lineas mas limpias)
SPEED_TRAVEL = 600        # lapiz arriba
ACCEL = 1200              # aceleracion base (grados/s^2)
SPEED_HOME = 200

# --- Lapiz ---
PEN_SPEED = 400
PEN_HOME_STALL = True     # True = al iniciar sube el lapiz hasta el tope y ese es "arriba"
PEN_HOME_DIR = -1         # -1 o 1: hacia donde gira para llegar al tope de "arriba"
PEN_DOWN_ANGLE = 120      # grados desde "arriba" hasta tocar el papel
PEN_DELAY_MS = 120        # pausa tras bajar/subir

SEG_MM = 0                # >0 divide lineas largas en tramos de ese largo (mm)

# ==========================================================================

ev3 = EV3Brick()
mx = Motor(PORT_X, DIR_X)
my = Motor(PORT_Y, DIR_Y)
mpen = Motor(PORT_PEN, DIR_PEN)
touch_x = TouchSensor(PORT_TOUCH_X) if PORT_TOUCH_X else None
touch_y = TouchSensor(PORT_TOUCH_Y) if PORT_TOUCH_Y else None

pen_is_down = False


def say(*lines):
    """Muestra texto en la pantalla (si el firmware lo permite) y en la consola."""
    print(*lines)
    try:
        ev3.screen.clear()
        for ln in lines:
            ev3.screen.print(ln)
    except Exception:
        pass


def motor_done(m):
    # Pybricks v3+/firmware nuevo: m.done(); Pybricks v2.0: m.control.done()
    try:
        return m.done()
    except AttributeError:
        return m.control.done()


def set_accel(m, accel):
    """Cambia solo la aceleracion, conservando los otros limites."""
    try:
        lim = m.control.limits()
        m.control.limits(lim[0], int(accel), lim[2])
    except Exception:
        pass


# ---------------------------------------------------------------- lapiz ---

def pen_up():
    global pen_is_down
    if pen_is_down:
        mpen.run_target(PEN_SPEED, 0, Stop.HOLD, True)
        wait(PEN_DELAY_MS)
        pen_is_down = False


def pen_down():
    global pen_is_down
    if not pen_is_down:
        mpen.run_target(PEN_SPEED, PEN_DOWN_ANGLE, Stop.HOLD, True)
        wait(PEN_DELAY_MS)
        pen_is_down = True


def home_pen():
    global pen_is_down
    if PEN_HOME_STALL:
        say("Home lapiz...")
        mpen.run_until_stalled(PEN_HOME_DIR * 200, Stop.HOLD, 40)
    mpen.reset_angle(0)
    pen_is_down = False


# ---------------------------------------------------------------- ejes ----

def home_axis(motor, sensor, name):
    """Lleva el eje hacia 0 hasta tocar el sensor. Sin sensor: posicion actual = 0."""
    if sensor is None:
        motor.reset_angle(0)
        return
    say("Home " + name + "...")
    if not sensor.pressed():
        motor.run(-SPEED_HOME)
        while not sensor.pressed():
            wait(5)
        motor.hold()
    # separarse del sensor y volver despacio para mas precision
    motor.run_angle(SPEED_HOME, 90, Stop.HOLD, True)
    motor.run(-SPEED_HOME // 3)
    while not sensor.pressed():
        wait(5)
    motor.hold()
    motor.reset_angle(0)


def clamp(v, lo, hi):
    return lo if v < lo else hi if v > hi else v


def _move_raw(tx, ty, speed):
    """Mueve ambos motores a la vez para que lleguen juntos (linea recta)."""
    dx = abs(tx - mx.angle())
    dy = abs(ty - my.angle())
    d = max(dx, dy)
    if d < 1:
        return
    rx = dx / d
    ry = dy / d
    # Velocidad Y aceleracion proporcionales: los dos perfiles duran lo mismo,
    # asi el lapiz sigue una recta tambien al acelerar/frenar.
    set_accel(mx, max(ACCEL * rx, 50))
    set_accel(my, max(ACCEL * ry, 50))
    mx.run_target(max(speed * rx, 10), tx, Stop.HOLD, False)
    my.run_target(max(speed * ry, 10), ty, Stop.HOLD, False)
    while not (motor_done(mx) and motor_done(my)):
        wait(2)


def move_to(x_mm, y_mm, speed):
    x_mm = clamp(x_mm, 0, MAX_X_MM)
    y_mm = clamp(y_mm, 0, MAX_Y_MM)
    tx = x_mm * DEG_PER_MM_X
    ty = y_mm * DEG_PER_MM_Y
    if SEG_MM > 0 and pen_is_down:
        cx = mx.angle() / DEG_PER_MM_X
        cy = my.angle() / DEG_PER_MM_Y
        length = ((x_mm - cx) ** 2 + (y_mm - cy) ** 2) ** 0.5
        n = int(length / SEG_MM)
        for i in range(1, n):
            f = i / n
            _move_raw((cx + (x_mm - cx) * f) * DEG_PER_MM_X,
                      (cy + (y_mm - cy) * f) * DEG_PER_MM_Y, speed)
    _move_raw(tx, ty, speed)


def to_machine(p):
    return (OFFSET_X_MM + p[0] * ESCALA, OFFSET_Y_MM + p[1] * ESCALA)


# ---------------------------------------------------------------- dibujo --

def draw(strokes):
    total = len(strokes)
    timer = StopWatch()
    for i, stroke in enumerate(strokes):
        if not stroke:
            continue
        if i % 5 == 0:
            say("Trazo %d/%d" % (i + 1, total), "Tiempo %ds" % (timer.time() // 1000))
        x, y = to_machine(stroke[0])
        pen_up()
        move_to(x, y, SPEED_TRAVEL)
        pen_down()
        for p in stroke[1:]:
            x, y = to_machine(p)
            move_to(x, y, SPEED_DRAW)
        # Pausa: si se presiona el boton central entre trazos
        if Button.CENTER in ev3.buttons.pressed():
            pen_up()
            say("PAUSA", "Centro = seguir")
            wait(500)
            while Button.CENTER not in ev3.buttons.pressed():
                wait(20)
            wait(500)
    pen_up()
    return timer.time() // 1000


def calibration_strokes():
    # cuadrado de 50 mm + diagonal + cruz de 20 mm
    return [
        [(0, 0), (50, 0), (50, 50), (0, 50), (0, 0)],
        [(0, 0), (50, 50)],
        [(15, 25), (35, 25)],
        [(25, 15), (25, 35)],
    ]


def main():
    if CALIBRAR:
        strokes = calibration_strokes()
        name = "Calibracion 50mm"
    else:
        from dibujo import STROKES, WIDTH, HEIGHT
        strokes = STROKES
        name = "Dibujo %dx%d mm" % (WIDTH, HEIGHT)

    say(name, "%d trazos" % len(strokes), "", "Centro = empezar")
    while Button.CENTER not in ev3.buttons.pressed():
        wait(20)
    while Button.CENTER in ev3.buttons.pressed():
        wait(20)

    home_pen()
    home_axis(mx, touch_x, "X")
    home_axis(my, touch_y, "Y")

    secs = draw(strokes)
    move_to(0, 0, SPEED_TRAVEL)
    say("Listo!", "%d s" % secs)
    ev3.speaker.beep(880, 200)
    wait(3000)


main()
