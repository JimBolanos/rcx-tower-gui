#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")] // sin consola en Windows

use eframe::egui::{self, Color32, Pos2, Rect, Sense, Shape, Stroke, Vec2};
use print3rbot_convertidor::trace::{self, Mode, Output, Settings};
use std::path::PathBuf;

const ACCENT: Color32 = Color32::from_rgb(0xd9, 0x63, 0x1e);
const PREVIEW_LINES: usize = 300;

fn main() -> eframe::Result {
    let options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default()
            .with_title("Convertidor Print3rbot")
            .with_inner_size([1150.0, 800.0])
            .with_min_inner_size([760.0, 520.0])
            .with_drag_and_drop(true),
        ..Default::default()
    };
    eframe::run_native("Convertidor Print3rbot", options, Box::new(|_cc| Ok(Box::new(App::new()))))
}

#[derive(Clone, PartialEq)]
struct Key {
    width_mm: f32,
    res: f32,
    threshold: u8,
    invert: bool,
    mode: Mode,
    hatch_mm: f32,
    cross: bool,
    simplify_mm: f32,
    min_len_mm: f32,
    flip_x: bool,
    flip_y: bool,
    image_id: u64,
}

impl Key {
    fn new(s: &Settings, image_id: u64) -> Self {
        Self {
            width_mm: s.width_mm,
            res: s.res,
            threshold: s.threshold,
            invert: s.invert,
            mode: s.mode,
            hatch_mm: s.hatch_mm,
            cross: s.cross,
            simplify_mm: s.simplify_mm,
            min_len_mm: s.min_len_mm,
            flip_x: s.flip_x,
            flip_y: s.flip_y,
            image_id,
        }
    }
}

struct App {
    settings: Settings,
    image: image::RgbaImage,
    image_name: String,
    image_id: u64,
    out: Option<Output>,
    code: String,
    code_preview: String,
    last_key: Option<Key>,
    texture: Option<(egui::TextureHandle, bool)>, // (textura, modo oscuro con el que se creó)
    show_image: bool,
    show_travel: bool,
    sim: Option<f32>,
    status: String,
}

impl App {
    fn new() -> Self {
        Self {
            settings: Settings::default(),
            image: trace::sample_image(),
            image_name: "Ejemplo: carita".into(),
            image_id: 0,
            out: None,
            code: String::new(),
            code_preview: String::new(),
            last_key: None,
            texture: None,
            show_image: true,
            show_travel: true,
            sim: None,
            status: String::new(),
        }
    }

    fn load(&mut self, path: PathBuf) {
        match image::open(&path) {
            Ok(img) => {
                self.image = img.to_rgba8();
                self.image_name = path.file_name().map(|n| n.to_string_lossy().into_owned()).unwrap_or_default();
                self.image_id += 1;
                self.status = String::new();
            }
            Err(e) => self.status = format!("No se pudo abrir la imagen: {e}. Prueba con PNG, JPG, BMP, GIF o WebP."),
        }
    }

    fn recompute_if_needed(&mut self) {
        let key = Key::new(&self.settings, self.image_id);
        if self.last_key.as_ref() == Some(&key) {
            return;
        }
        let out = trace::convert(&self.image, &self.settings);
        self.code = trace::to_python(&out);
        let mut lines = self.code.lines();
        let mut preview: Vec<&str> = lines.by_ref().take(PREVIEW_LINES).collect();
        if lines.next().is_some() {
            preview.push("    # ... (vista previa recortada; Copiar y Guardar incluyen todo)");
        }
        self.code_preview = preview.join("\n");
        self.out = Some(out);
        self.texture = None;
        self.sim = None;
        self.last_key = Some(key);
    }

    fn save(&mut self) {
        if let Some(path) = rfd::FileDialog::new()
            .set_title("Guardar dibujo.py")
            .set_file_name("dibujo.py")
            .add_filter("Python", &["py"])
            .save_file()
        {
            self.status = match std::fs::write(&path, &self.code) {
                Ok(()) => format!("Guardado en {}", path.display()),
                Err(e) => format!("No se pudo guardar: {e}"),
            };
        }
    }

    fn controls(&mut self, ui: &mut egui::Ui) {
        ui.add_space(4.0);
        section(ui, "IMAGEN");
        if ui.button("Abrir imagen…").clicked() {
            if let Some(p) = rfd::FileDialog::new()
                .add_filter("Imágenes", &["png", "jpg", "jpeg", "bmp", "gif", "webp"])
                .pick_file()
            {
                self.load(p);
                return;
            }
        }
        ui.label(egui::RichText::new(format!("{}  ·  o arrastra una imagen a la ventana", self.image_name)).weak().small());

        let s = &mut self.settings;
        section(ui, "TAMAÑO EN EL PAPEL");
        ui.horizontal(|ui| {
            ui.label("Ancho");
            ui.add(egui::DragValue::new(&mut s.width_mm).range(10.0..=300.0).suffix(" mm").speed(0.5));
            if let Some(o) = &self.out {
                ui.label(egui::RichText::new(format!("alto {:.1} mm", o.height_mm)).weak());
            }
        });
        ui.add(egui::Slider::new(&mut s.res, 1.0..=6.0).step_by(0.5).text("px por mm"));

        section(ui, "BLANCO Y NEGRO");
        ui.add(egui::Slider::new(&mut s.threshold, 10..=245).text("umbral"));
        ui.horizontal(|ui| {
            ui.checkbox(&mut s.invert, "Invertir");
            ui.checkbox(&mut self.show_image, "Ver imagen");
        });

        section(ui, "TRAZOS");
        ui.horizontal(|ui| {
            ui.selectable_value(&mut s.mode, Mode::Contour, "Contorno");
            ui.selectable_value(&mut s.mode, Mode::Fill, "Relleno");
            ui.selectable_value(&mut s.mode, Mode::Both, "Ambos");
        });
        if s.mode != Mode::Contour {
            ui.add(egui::Slider::new(&mut s.hatch_mm, 0.5..=5.0).step_by(0.1).suffix(" mm").text("rayado"));
            ui.checkbox(&mut s.cross, "Rayado cruzado");
        }
        ui.add(egui::Slider::new(&mut s.simplify_mm, 0.0..=1.5).step_by(0.05).suffix(" mm").text("simplificar"));
        ui.add(egui::Slider::new(&mut s.min_len_mm, 0.0..=10.0).step_by(0.5).suffix(" mm").text("trazo mínimo"));

        section(ui, "ORIENTACIÓN");
        ui.horizontal(|ui| {
            ui.checkbox(&mut s.flip_x, "Espejo X");
            ui.checkbox(&mut s.flip_y, "Espejo Y");
        });
        ui.checkbox(&mut self.show_travel, "Ver viajes (lápiz arriba)");

        if let Some(o) = &self.out {
            let st = trace::stats(&o.strokes);
            section(ui, "RESULTADO");
            egui::Grid::new("stats").num_columns(2).spacing([16.0, 2.0]).show(ui, |ui| {
                ui.label("Trazos");
                ui.monospace(st.strokes.to_string());
                ui.end_row();
                ui.label("Puntos");
                let pts = egui::RichText::new(st.points.to_string()).monospace();
                ui.label(if st.points > 20000 { pts.color(ACCENT) } else { pts });
                ui.end_row();
                ui.label("Tinta");
                ui.monospace(format!("{:.2} m", st.ink_mm / 1000.0));
                ui.end_row();
                ui.label("Tiempo aprox.");
                ui.monospace(trace::fmt_time(st.seconds));
                ui.end_row();
            });
            if st.points > 20000 {
                ui.colored_label(ACCENT, "Muchos puntos: sube «simplificar» o baja los px por mm.");
            }
        }
    }

    fn preview(&mut self, ui: &mut egui::Ui) {
        let Some(o) = &self.out else { return };
        let dark = ui.visuals().dark_mode;
        let (paper, grid, grid_strong, ink, muted) = if dark {
            (
                Color32::from_rgb(0x1b, 0x24, 0x30),
                Color32::from_rgb(0x26, 0x32, 0x40),
                Color32::from_rgb(0x39, 0x49, 0x5c),
                Color32::from_rgb(0xe4, 0xeb, 0xf3),
                Color32::from_rgb(0x93, 0xa3, 0xb6),
            )
        } else {
            (
                Color32::from_rgb(0xfb, 0xfc, 0xfd),
                Color32::from_rgb(0xd5, 0xdf, 0xe9),
                Color32::from_rgb(0xa9, 0xbd, 0xd1),
                Color32::from_rgb(0x16, 0x24, 0x3a),
                Color32::from_rgb(0x5b, 0x6b, 0x80),
            )
        };

        let avail = ui.available_size();
        let pad = 30.0;
        let scale = ((avail.x - 2.0 * pad) / o.width_mm).min((avail.y - 2.0 * pad) / o.height_mm).max(0.5);
        let size = Vec2::new(o.width_mm * scale + 2.0 * pad, o.height_mm * scale + 2.0 * pad);
        let (resp, painter) = ui.allocate_painter(size, Sense::hover());
        let origin = resp.rect.min + Vec2::splat(pad);
        let p = |x: f32, y: f32| Pos2::new(origin.x + x * scale, origin.y + y * scale);

        painter.rect_filled(resp.rect, 4.0, paper);
        let max_mm = o.width_mm.max(o.height_mm).ceil() as i32;
        for m in 0..=max_mm {
            let major = m % 10 == 0;
            if !major && scale < 4.0 {
                continue;
            }
            let st = Stroke::new(if major { 1.0f32 } else { 0.5 }, if major { grid_strong } else { grid });
            let mf = m as f32;
            if mf <= o.width_mm {
                painter.line_segment([p(mf, 0.0), p(mf, o.height_mm)], st);
                if major {
                    painter.text(p(mf, 0.0) - Vec2::new(0.0, 8.0), egui::Align2::CENTER_BOTTOM, m.to_string(), egui::FontId::monospace(10.0), muted);
                }
            }
            if mf <= o.height_mm {
                painter.line_segment([p(0.0, mf), p(o.width_mm, mf)], st);
                if major {
                    painter.text(p(0.0, mf) - Vec2::new(8.0, 0.0), egui::Align2::RIGHT_CENTER, m.to_string(), egui::FontId::monospace(10.0), muted);
                }
            }
        }

        if self.show_image {
            let need_new = !matches!(&self.texture, Some((_, d)) if *d == dark);
            if need_new {
                let bm = &o.bitmap;
                let mut rgba = vec![0u8; bm.w * bm.h * 4];
                for (i, &v) in bm.px.iter().enumerate() {
                    let (mut x, mut y) = (i % bm.w, i / bm.w);
                    if self.settings.flip_x {
                        x = bm.w - 1 - x;
                    }
                    if self.settings.flip_y {
                        y = bm.h - 1 - y;
                    }
                    let o4 = (y * bm.w + x) * 4;
                    rgba[o4..o4 + 4].copy_from_slice(&[ink.r(), ink.g(), ink.b(), if v == 1 { 30 } else { 0 }]);
                }
                let img = egui::ColorImage::from_rgba_unmultiplied([bm.w, bm.h], &rgba);
                self.texture = Some((ui.ctx().load_texture("bitmap", img, egui::TextureOptions::NEAREST), dark));
            }
            if let Some((tex, _)) = &self.texture {
                let r = Rect::from_min_max(p(0.0, 0.0), p(o.width_mm, o.height_mm));
                painter.image(tex.id(), r, Rect::from_min_max(Pos2::ZERO, Pos2::new(1.0, 1.0)), Color32::WHITE);
            }
        }

        painter.circle_filled(p(0.0, 0.0), 4.0, ACCENT);

        let n = self.sim.map_or(o.strokes.len(), |v| (v as usize).min(o.strokes.len()));
        if self.show_travel {
            let travel = Stroke::new(0.8f32, ACCENT.gamma_multiply(0.6));
            let (mut cx, mut cy) = (0.0, 0.0);
            for s in &o.strokes[..n] {
                painter.extend(Shape::dashed_line(&[p(cx, cy), p(s[0][0], s[0][1])], travel, 3.0, 3.0));
                let e = s[s.len() - 1];
                cx = e[0];
                cy = e[1];
            }
        }
        let pen = Stroke::new((0.35 * scale).max(1.0), ink);
        for s in &o.strokes[..n] {
            painter.add(Shape::line(s.iter().map(|q| p(q[0], q[1])).collect(), pen));
        }
        if n > 0 && n < o.strokes.len() {
            let e = o.strokes[n - 1][o.strokes[n - 1].len() - 1];
            painter.circle_filled(p(e[0], e[1]), 5.0, ACCENT);
        }
    }
}

fn section(ui: &mut egui::Ui, title: &str) {
    ui.add_space(10.0);
    ui.label(egui::RichText::new(title).small().strong().weak());
    ui.add_space(2.0);
}

impl eframe::App for App {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        // arrastrar y soltar
        let dropped = ctx.input(|i| i.raw.dropped_files.iter().find_map(|f| f.path.clone()));
        if let Some(path) = dropped {
            self.load(path);
        }
        self.recompute_if_needed();

        egui::SidePanel::left("controles").resizable(false).exact_width(300.0).show(ctx, |ui| {
            egui::ScrollArea::vertical().show(ui, |ui| self.controls(ui));
        });

        egui::TopBottomPanel::bottom("codigo").resizable(true).default_height(220.0).show(ctx, |ui| {
            ui.add_space(6.0);
            ui.horizontal(|ui| {
                if ui.add(egui::Button::new(egui::RichText::new("Guardar dibujo.py…").color(Color32::WHITE)).fill(ACCENT)).clicked() {
                    self.save();
                }
                if ui.button("Copiar").clicked() {
                    ctx.copy_text(self.code.clone());
                    self.status = "dibujo.py copiado al portapapeles.".into();
                }
                let sim_label = if self.sim.is_some() { "Detener" } else { "Simular dibujo" };
                if ui.button(sim_label).clicked() {
                    self.sim = if self.sim.is_some() { None } else { Some(0.0) };
                }
                ui.label(egui::RichText::new(&self.status).color(ACCENT));
            });
            ui.label(egui::RichText::new("Pon dibujo.py junto a print3rbot.py en tu proyecto de Pybricks. (0, 0) = home del robot; la cuadrícula marca 10 mm.").weak().small());
            ui.add_space(4.0);
            egui::ScrollArea::both().auto_shrink([false, false]).show(ui, |ui| {
                ui.add(egui::Label::new(egui::RichText::new(&self.code_preview).monospace()).extend().selectable(true));
            });
        });

        egui::CentralPanel::default().show(ctx, |ui| {
            ui.centered_and_justified(|ui| self.preview(ui));
        });

        if let (Some(v), Some(o)) = (self.sim, &self.out) {
            let total = o.strokes.len() as f32;
            let next = v + (total / 240.0).max(1.0);
            self.sim = if next >= total { None } else { Some(next) };
            ctx.request_repaint();
        }
    }
}
