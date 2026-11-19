//! Conversión imagen -> trazos en milímetros. Sin dependencias de la interfaz.

use std::collections::HashMap;

pub type Pt = [f32; 2];
pub type Stroke = Vec<Pt>;

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum Mode {
    Contour,
    Fill,
    Both,
}

#[derive(Clone, Debug)]
pub struct Settings {
    pub width_mm: f32,
    /// píxeles por mm
    pub res: f32,
    pub threshold: u8,
    pub invert: bool,
    pub mode: Mode,
    pub hatch_mm: f32,
    pub cross: bool,
    pub simplify_mm: f32,
    pub min_len_mm: f32,
    pub flip_x: bool,
    pub flip_y: bool,
}

impl Default for Settings {
    fn default() -> Self {
        Self {
            width_mm: 80.0,
            res: 3.0,
            threshold: 128,
            invert: false,
            mode: Mode::Contour,
            hatch_mm: 1.2,
            cross: false,
            simplify_mm: 0.25,
            min_len_mm: 1.5,
            flip_x: false,
            flip_y: false,
        }
    }
}

/// Imagen binaria: 1 = tinta.
pub struct Bitmap {
    pub w: usize,
    pub h: usize,
    pub px: Vec<u8>,
}

impl Bitmap {
    #[inline]
    pub fn get(&self, x: i64, y: i64) -> u8 {
        if x < 0 || y < 0 || x >= self.w as i64 || y >= self.h as i64 {
            0
        } else {
            self.px[y as usize * self.w + x as usize]
        }
    }
}

pub struct Output {
    pub strokes: Vec<Stroke>,
    pub width_mm: f32,
    pub height_mm: f32,
    pub bitmap: Bitmap,
}

/// Escala la imagen al tamaño de trabajo y la pasa a blanco y negro.
pub fn binarize(img: &image::RgbaImage, s: &Settings) -> (Bitmap, f32) {
    let (iw, ih) = img.dimensions();
    let height_mm = s.width_mm * ih as f32 / iw.max(1) as f32;
    let bw = ((s.width_mm * s.res).round() as u32).max(4);
    let bh = ((height_mm * s.res).round() as u32).max(4);
    let small = image::imageops::resize(img, bw, bh, image::imageops::FilterType::Triangle);
    let mut px = Vec::with_capacity((bw * bh) as usize);
    for p in small.pixels() {
        let [r, g, b, a] = p.0;
        let a = a as f32 / 255.0;
        let lum = (0.299 * r as f32 + 0.587 * g as f32 + 0.114 * b as f32) * a + 255.0 * (1.0 - a);
        let mut ink = lum < s.threshold as f32;
        if s.invert {
            ink = !ink;
        }
        px.push(ink as u8);
    }
    (Bitmap { w: bw as usize, h: bh as usize, px }, height_mm)
}

/// Contornos cerrados con marching squares. Coordenadas en píxeles (centro de píxel = entero).
pub fn contours(bm: &Bitmap) -> Vec<Vec<[f32; 2]>> {
    // Puntos en coordenadas dobladas (enteros) para que las uniones sean exactas.
    let mut adj: HashMap<(i64, i64), Vec<(i64, i64)>> = HashMap::new();
    let mut link = |a: (i64, i64), b: (i64, i64)| {
        adj.entry(a).or_default().push(b);
        adj.entry(b).or_default().push(a);
    };
    for y in -1..bm.h as i64 {
        for x in -1..bm.w as i64 {
            let tl = bm.get(x, y);
            let tr = bm.get(x + 1, y);
            let br = bm.get(x + 1, y + 1);
            let bl = bm.get(x, y + 1);
            let c = tl * 8 + tr * 4 + br * 2 + bl;
            if c == 0 || c == 15 {
                continue;
            }
            let t = (2 * x + 1, 2 * y);
            let r = (2 * x + 2, 2 * y + 1);
            let b = (2 * x + 1, 2 * y + 2);
            let l = (2 * x, 2 * y + 1);
            match c {
                1 | 14 => link(l, b),
                2 | 13 => link(b, r),
                3 | 12 => link(l, r),
                4 | 11 => link(t, r),
                6 | 9 => link(t, b),
                7 | 8 => link(l, t),
                5 => {
                    link(l, t);
                    link(b, r);
                }
                10 => {
                    link(t, r);
                    link(l, b);
                }
                _ => {}
            }
        }
    }
    // orden determinista
    let mut starts: Vec<(i64, i64)> = adj.keys().copied().collect();
    starts.sort_unstable();
    let mut out = Vec::new();
    for start in starts {
        while adj.get(&start).map_or(false, |v| !v.is_empty()) {
            let mut pts = vec![start];
            let mut cur = start;
            loop {
                let nb = match adj.get_mut(&cur).and_then(|v| v.pop()) {
                    Some(n) => n,
                    None => break,
                };
                if let Some(back) = adj.get_mut(&nb) {
                    if let Some(i) = back.iter().position(|&p| p == cur) {
                        back.swap_remove(i);
                    }
                }
                pts.push(nb);
                cur = nb;
                if cur == start {
                    break;
                }
            }
            out.push(pts.iter().map(|&(x, y)| [x as f32 / 2.0, y as f32 / 2.0]).collect());
        }
    }
    out
}

/// Rayado de las zonas con tinta. `step_px` = separación entre líneas en píxeles.
pub fn hatch(bm: &Bitmap, step_px: f32, vertical: bool) -> Vec<Vec<[f32; 2]>> {
    let (n1, n2) = if vertical { (bm.w, bm.h) } else { (bm.h, bm.w) };
    let at = |i: usize, j: usize| if vertical { bm.px[j * bm.w + i] } else { bm.px[i * bm.w + j] };
    let mut out = Vec::new();
    let mut flip = false;
    let mut f = step_px / 2.0;
    let step = step_px.max(0.5);
    while f < n1 as f32 {
        let i = (f.round() as usize).min(n1 - 1);
        let mut row = Vec::new();
        let mut s: Option<usize> = None;
        for j in 0..=n2 {
            let on = j < n2 && at(i, j) == 1;
            match (on, s) {
                (true, None) => s = Some(j),
                (false, Some(st)) => {
                    row.push((st as f32 - 0.3, j as f32 - 0.7));
                    s = None;
                }
                _ => {}
            }
        }
        if flip {
            row.reverse();
        }
        for (a, b) in row {
            let mut p = if vertical {
                vec![[i as f32, a], [i as f32, b]]
            } else {
                vec![[a, i as f32], [b, i as f32]]
            };
            if flip {
                p.reverse();
            }
            out.push(p);
        }
        flip = !flip;
        f += step;
    }
    out
}

fn seg_dist(p: Pt, a: Pt, b: Pt) -> f32 {
    let (dx, dy) = (b[0] - a[0], b[1] - a[1]);
    let l = (dx * dx + dy * dy).sqrt();
    if l < 1e-9 {
        ((p[0] - a[0]).powi(2) + (p[1] - a[1]).powi(2)).sqrt()
    } else {
        (dy * p[0] - dx * p[1] + b[0] * a[1] - b[1] * a[0]).abs() / l
    }
}

/// Ramer-Douglas-Peucker iterativo.
pub fn rdp(pts: &[Pt], eps: f32) -> Stroke {
    if pts.len() < 3 || eps <= 0.0 {
        return pts.to_vec();
    }
    let mut keep = vec![false; pts.len()];
    keep[0] = true;
    keep[pts.len() - 1] = true;
    let mut stack = vec![(0usize, pts.len() - 1)];
    while let Some((a, b)) = stack.pop() {
        let (mut md, mut mi) = (-1.0f32, 0usize);
        for i in a + 1..b {
            let d = seg_dist(pts[i], pts[a], pts[b]);
            if d > md {
                md = d;
                mi = i;
            }
        }
        if md > eps {
            keep[mi] = true;
            stack.push((a, mi));
            stack.push((mi, b));
        }
    }
    pts.iter().zip(keep).filter(|(_, k)| *k).map(|(p, _)| *p).collect()
}

/// Simplifica un lazo cerrado partiéndolo en el punto más lejano al inicio.
pub fn simplify_closed(pts: &[Pt], eps: f32) -> Stroke {
    if pts.len() < 4 {
        return pts.to_vec();
    }
    let p0 = pts[0];
    let far = (0..pts.len())
        .max_by(|&i, &j| {
            let di = (pts[i][0] - p0[0]).hypot(pts[i][1] - p0[1]);
            let dj = (pts[j][0] - p0[0]).hypot(pts[j][1] - p0[1]);
            di.total_cmp(&dj)
        })
        .unwrap_or(0);
    let mut a = rdp(&pts[..=far], eps);
    let b = rdp(&pts[far..], eps);
    a.extend_from_slice(&b[1..]);
    a
}

pub fn length(s: &[Pt]) -> f32 {
    s.windows(2).map(|w| (w[1][0] - w[0][0]).hypot(w[1][1] - w[0][1])).sum()
}

/// Ordena por vecino más cercano (empezando en 0,0), invirtiendo trazos si conviene.
pub fn order(strokes: Vec<Stroke>) -> Vec<Stroke> {
    let mut left = strokes;
    let mut out = Vec::with_capacity(left.len());
    let (mut cx, mut cy) = (0.0f32, 0.0f32);
    while !left.is_empty() {
        let (mut bi, mut bd, mut brev) = (0usize, f32::INFINITY, false);
        for (i, s) in left.iter().enumerate() {
            let a = s[0];
            let b = s[s.len() - 1];
            let da = (a[0] - cx).powi(2) + (a[1] - cy).powi(2);
            if da < bd {
                bd = da;
                bi = i;
                brev = false;
            }
            let db = (b[0] - cx).powi(2) + (b[1] - cy).powi(2);
            if db < bd {
                bd = db;
                bi = i;
                brev = true;
            }
        }
        let mut s = left.swap_remove(bi);
        if brev {
            s.reverse();
        }
        let e = s[s.len() - 1];
        cx = e[0];
        cy = e[1];
        out.push(s);
    }
    out
}

/// Pipeline completo.
pub fn convert(img: &image::RgbaImage, s: &Settings) -> Output {
    let (bm, height_mm) = binarize(img, s);
    let w = s.width_mm;
    let to_mm = |p: [f32; 2]| -> Pt {
        let mut x = (p[0] + 0.5) / s.res;
        let mut y = (p[1] + 0.5) / s.res;
        if s.flip_x {
            x = w - x;
        }
        if s.flip_y {
            y = height_mm - y;
        }
        [x.clamp(0.0, w), y.clamp(0.0, height_mm)]
    };
    let mut strokes: Vec<Stroke> = Vec::new();
    if s.mode != Mode::Fill {
        for c in contours(&bm) {
            let mm: Stroke = c.into_iter().map(to_mm).collect();
            let st = simplify_closed(&mm, s.simplify_mm);
            if st.len() >= 2 && length(&st) >= s.min_len_mm {
                strokes.push(st);
            }
        }
    }
    if s.mode != Mode::Contour {
        let step = s.hatch_mm * s.res;
        let mut h = hatch(&bm, step, false);
        if s.cross {
            h.extend(hatch(&bm, step, true));
        }
        for st in h {
            let mm: Stroke = st.into_iter().map(to_mm).collect();
            if length(&mm) >= s.min_len_mm.min(0.5) {
                strokes.push(mm);
            }
        }
    }
    Output { strokes: order(strokes), width_mm: w, height_mm, bitmap: bm }
}

pub struct Stats {
    pub strokes: usize,
    pub points: usize,
    pub ink_mm: f32,
    pub seconds: f32,
}

pub fn stats(strokes: &[Stroke]) -> Stats {
    let (mut points, mut ink, mut travel) = (0, 0.0, 0.0);
    let (mut cx, mut cy) = (0.0f32, 0.0f32);
    for s in strokes {
        points += s.len();
        ink += length(s);
        travel += (s[0][0] - cx).hypot(s[0][1] - cy);
        let e = s[s.len() - 1];
        cx = e[0];
        cy = e[1];
    }
    // Valores por defecto de print3rbot.py: 12 °/mm, 250 °/s dibujando, 600 °/s viajando.
    let seconds = ink / (250.0 / 12.0) + travel / (600.0 / 12.0) + strokes.len() as f32 * 0.9;
    Stats { strokes: strokes.len(), points, ink_mm: ink, seconds }
}

pub fn fmt_time(secs: f32) -> String {
    if secs < 60.0 {
        format!("{} s", secs.round() as i64)
    } else {
        format!("{} min {} s", (secs / 60.0) as i64, (secs % 60.0).round() as i64)
    }
}

fn fmt_num(v: f32) -> String {
    let r = (v * 10.0).round() / 10.0;
    if r.fract() == 0.0 {
        format!("{}", r as i64)
    } else {
        format!("{:.1}", r)
    }
}

/// Genera el contenido de dibujo.py.
pub fn to_python(o: &Output) -> String {
    let st = stats(&o.strokes);
    let mut s = String::new();
    s.push_str("# dibujo.py - generado por el Convertidor Print3rbot\n");
    s.push_str(&format!(
        "# {} trazos, {} puntos, ~{}\n# Unidades: mm. (0, 0) = home del robot.\n\n",
        st.strokes,
        st.points,
        fmt_time(st.seconds)
    ));
    s.push_str(&format!("WIDTH = {}\nHEIGHT = {}\n\nSTROKES = [\n", fmt_num(o.width_mm), fmt_num(o.height_mm)));
    for stroke in &o.strokes {
        let mut parts: Vec<String> = Vec::new();
        for p in stroke {
            let t = format!("({},{})", fmt_num(p[0]), fmt_num(p[1]));
            if parts.last() != Some(&t) {
                parts.push(t);
            }
        }
        if parts.len() == 1 {
            parts.push(parts[0].clone());
        }
        s.push_str("    [");
        s.push_str(&parts.join(","));
        s.push_str("],\n");
    }
    s.push_str("]\n");
    s
}

/// Imagen de ejemplo: una carita.
pub fn sample_image() -> image::RgbaImage {
    let n = 400u32;
    image::RgbaImage::from_fn(n, n, |x, y| {
        let (fx, fy) = (x as f32, y as f32);
        let d = (fx - 200.0).hypot(fy - 200.0);
        let ring = (d - 170.0).abs() < 13.0;
        let eye = |cx: f32| ((fx - cx) / 28.0).powi(2) + ((fy - 160.0) / 40.0).powi(2) < 1.0;
        let ds = (fx - 200.0).hypot(fy - 215.0);
        let ang = (fy - 215.0).atan2(fx - 200.0);
        let smile = (ds - 95.0).abs() < 11.0 && ang > 0.15 * std::f32::consts::PI && ang < 0.85 * std::f32::consts::PI;
        if ring || eye(140.0) || eye(260.0) || smile {
            image::Rgba([0, 0, 0, 255])
        } else {
            image::Rgba([255, 255, 255, 255])
        }
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn square_img() -> image::RgbaImage {
        // cuadrado negro de 50x50 dentro de 100x100
        image::RgbaImage::from_fn(100, 100, |x, y| {
            if (25..75).contains(&x) && (25..75).contains(&y) {
                image::Rgba([0, 0, 0, 255])
            } else {
                image::Rgba([255, 255, 255, 255])
            }
        })
    }

    #[test]
    fn square_contour_is_one_closed_loop_of_right_size() {
        let s = Settings { width_mm: 100.0, res: 1.0, ..Default::default() };
        let o = convert(&square_img(), &s);
        assert_eq!(o.strokes.len(), 1);
        let st = &o.strokes[0];
        assert_eq!(st.first(), st.last(), "contorno cerrado");
        // perímetro ~ 4 * 50 mm
        let l = length(st);
        assert!((l - 200.0).abs() < 6.0, "perímetro {}", l);
        // simplificado: un cuadrado necesita pocos puntos
        assert!(st.len() <= 10, "puntos {}", st.len());
    }

    #[test]
    fn fill_hatches_cover_the_square() {
        let s = Settings { width_mm: 100.0, res: 1.0, mode: Mode::Fill, hatch_mm: 5.0, ..Default::default() };
        let o = convert(&square_img(), &s);
        assert_eq!(o.strokes.len(), 10);
        for st in &o.strokes {
            assert_eq!(st.len(), 2);
            assert!((length(st) - 49.0).abs() < 1.5);
        }
    }

    #[test]
    fn blank_image_gives_no_strokes() {
        let img = image::RgbaImage::from_pixel(50, 50, image::Rgba([255, 255, 255, 255]));
        let o = convert(&img, &Settings::default());
        assert!(o.strokes.is_empty());
        assert!(to_python(&o).contains("STROKES = [\n]"));
    }

    #[test]
    fn ring_gives_two_contours() {
        let o = convert(&sample_image(), &Settings::default());
        // aro (2) + ojos (2) + sonrisa (1)
        assert_eq!(o.strokes.len(), 5);
        assert!((o.height_mm - 80.0).abs() < 0.01);
    }

    #[test]
    fn rdp_keeps_endpoints_and_corners() {
        let pts: Vec<Pt> = vec![[0., 0.], [1., 0.01], [2., 0.], [2., 1.], [2., 2.]];
        assert_eq!(rdp(&pts, 0.1), vec![[0., 0.], [2., 0.], [2., 2.]]);
    }

    #[test]
    fn order_starts_near_origin_and_reverses() {
        let a = vec![[10., 10.], [0.5, 0.5]];
        let b = vec![[50., 50.], [60., 60.]];
        let o = order(vec![b, a]);
        assert_eq!(o[0][0], [0.5, 0.5]);
        assert_eq!(o[1][0], [50., 50.]);
    }

    #[test]
    fn python_formatting() {
        let o = Output {
            strokes: vec![vec![[1.04, 2.0], [1.0, 2.0], [3.25, 4.0]]],
            width_mm: 60.0,
            height_mm: 45.5,
            bitmap: Bitmap { w: 0, h: 0, px: vec![] },
        };
        let py = to_python(&o);
        assert!(py.contains("WIDTH = 60\nHEIGHT = 45.5"));
        assert!(py.contains("    [(1,2),(3.3,4)],"), "{}", py);
    }
}
