# EV3 Print3rbot con Pybricks

Programa de Pybricks para el **Print3rbot** de Christophe Avenel (2015): un brazo de dibujo
con dos brazos articulados unidos en el lápiz (five-bar / SCARA paralelo), hecho con el
EV3 Home (31313) más un segundo sensor de toque. Incluye un convertidor de imagen → trazos.

| Archivo | Qué es |
|---|---|
| `print3rbot.py` | Programa del robot (Pybricks, EV3). |
| `dibujo.py` | El dibujo: lista de trazos en mm. Trae un ejemplo (marco + estrella + círculo). |
| `convertidor.html` | Convertidor en el navegador: imagen → `dibujo.py`. |
| `convertidor-rs/` | El mismo convertidor, nativo para Windows, macOS y Linux (Rust). |

## Cómo funciona el robot

- Cada motor grande mueve un **tornillo sinfín** que gira un **engranaje de 40 dientes**: el
  "hombro" de cada brazo. 40 vueltas del motor = 1 vuelta del brazo.
- Cada brazo tiene dos tramos: **brazo** (hombro → codo, donde están los bujes amarillos) y
  **antebrazo** (codo → lápiz). Los dos antebrazos se unen en el portalápiz.
- El programa calcula los ángulos de los dos hombros para cada punto (cinemática inversa) y
  mueve el lápiz en línea recta a velocidad constante.
- El motor mediano sube y baja el lápiz. Los sensores de toque dan el punto de partida (home).

## Conexiones (de las instrucciones)

| Puerto | Pieza |
|---|---|
| C | Motor grande izquierdo |
| A | Motor grande derecho |
| B | Motor mediano (lápiz) |
| 2 | Sensor de toque izquierdo |
| 3 | Sensor de toque derecho |

## 1. Cargar el programa (code.pybricks.com)

1. Abre code.pybricks.com en Chrome o Edge y conecta el EV3.
2. Crea un archivo llamado exactamente `dibujo.py` y pega el dibujo.
3. Crea `print3rbot.py` y pega el programa.
4. Con `print3rbot.py` abierto, aprieta **Run**. `dibujo.py` se envía solo.

## 2. Medir el robot (una vez)

Con una regla, de **centro de eje a centro de eje** (en una viga hay 8 mm entre agujeros):

| Variable | Qué medir | Valor inicial |
|---|---|---|
| `D` | Entre los dos ejes verticales de los engranajes de 40 dientes | 40 |
| `L1` | Eje del hombro → eje del codo (bujes amarillos) | 80 |
| `L2` | Eje del codo → centro del lápiz | 100 |

Los valores iniciales son estimaciones hechas a partir de los dibujos de las instrucciones.
Pon tus medidas reales.

## 3. Calibrar

Cada vez que corre, el programa primero pide **ajustar el lápiz**: con los botones
Arriba/Abajo del EV3 mueve el motor mediano hasta que el lápiz toque el papel y aprieta Centro.
(Si "Arriba" baja el lápiz, cambia el signo de `PEN_UP_ANGLE`. Si sube poco, aumenta su valor.)

**Paso A: `CALIBRAR = 1` (home y brazos al frente)**

1. Los brazos giran hasta tocar los sensores. Si un brazo se aleja de su sensor o el
   programa dice "Home falló", cambia su `HOME_DIR_LEFT` o `HOME_DIR_RIGHT` (1 ↔ -1).
2. Luego los dos brazos (tramo hombro → codo) deben quedar **apuntando al frente,
   paralelos**.
   - Si un brazo gira para el lado contrario al esperado, cambia su `DIR_LEFT` o `DIR_RIGHT`.
   - Si queda girado unos grados **hacia la izquierda**, **suma** esos grados a su
     `HOME_ANGLE_*`. Si queda **hacia la derecha**, **réstalos**.
   - Repite hasta que queden bien.

**Paso B: `CALIBRAR = 2` (rectángulo de prueba)**

Dibuja el borde del área (`AREA_W` × `AREA_H`, 60 × 45 mm) con una cruz al centro.
Si sale más grande o más chico, o deformado, revisa `D`, `L1` y `L2`. Si dice "fuera de
alcance", achica el área o cambia `AREA_CY` (distancia del área a los hombros).

**Paso C: `CALIBRAR = 0`** → dibuja `dibujo.py`.

## 4. Dibujar

1. En el convertidor, sube la imagen y elige el modo:
   - **Contorno**: los bordes de las zonas negras (siluetas y logos).
   - **Relleno**: rayado dentro de las zonas negras.
   - **Ambos**: borde + relleno.
2. Copia o guarda el resultado como `dibujo.py`.
3. Corre `print3rbot.py`. El dibujo se escala solo para llenar el área (`AJUSTAR = True`).
4. Durante el dibujo, el botón central pausa entre trazos.

Consejos: imágenes de alto contraste funcionan mejor. Si las líneas tiemblan, baja
`SPEED_DRAW`. El portalápiz está en thingiverse.com/thing:409421 (indicado en las
instrucciones).
