using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public enum SpinnerLook
    {
        /// <summary>The Material/Chrome classic: one arc whose sweep breathes while rotating.</summary>
        Arc,
        /// <summary>Two counter-rotating arcs on different radii - accent outside, soft inside.</summary>
        DualRing,
        /// <summary>Orbiting dots, each breathing in size with a phase offset.</summary>
        Dots,
        /// <summary>A ring of flat segments with a fading tail chasing around.</summary>
        Segments,
        /// <summary>Three staggered arcs that breathe independently across nested rings.</summary>
        TripleArc,
        /// <summary>A softly pulsing bead chase travelling around a square path.</summary>
        SquareChase,
        /// <summary>A breathing orbital marker followed by a graduated echo trail.</summary>
        OrbitEcho,
        /// <summary>A rotating polygon whose alternating vertices flex in and out.</summary>
        PolygonPulse,
        /// <summary>A radial bank of bars rippling like a compact circular equaliser.</summary>
        EqualizerRing,
        /// <summary>A figure-8 lemniscate path where two breathing beads traverse opposite loops.</summary>
        InfinityChase,
        /// <summary>A rotating ring of custom dashes whose dash offset and gap ratios continuously flow.</summary>
        FlowingDash,
        /// <summary>Concentric circular rings expanding outward and fading in a continuous ripple.</summary>
        ConcentricRipples,
        /// <summary>A circular pie sector wipe that breathes in sweep while rotating on its axis.</summary>
        PieWipe,
        /// <summary>A rod-and-bob pendulum that eases at each extremum like simple harmonic motion.</summary>
        Pendulum,
        /// <summary>Five hanging beads that trade a single eased impulse, end to end.</summary>
        NewtonCradle,
        /// <summary>A stroked silhouette that eases from circle to triangle to square and back.</summary>
        MorphCycle,
        /// <summary>A DNA double-helix whose counter-phased strands breathe while rungs travel.</summary>
        DnaHelix,
        /// <summary>Six camera iris blades rotating while the aperture eases open and closed.</summary>
        IrisShutter,
        /// <summary>Three nested orbits of trailing orbs - TripleArc's structure in OrbitEcho's skin, no rings.</summary>
        TripleOrbit,
    }

    /// <summary>
    /// A flat, fully procedural spinner - no image assets, no pixels. Everything is drawn
    /// as geometry each frame, so it stays crisp at any size and DPI and recolours from
    /// properties. The "alive" feel comes from easing: motion runs through a cosine
    /// ease-in-out rather than linearly, which is most of what separates smart-looking
    /// spinners from a rotating ball on a stick.
    ///
    /// The animation timer only runs while the control is visible AND Spinning - a hidden
    /// or stopped spinner costs nothing.
    /// </summary>
    [ToolboxItem(true)]
    public class SpinnerBox : Control
    {
        private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };   // ~60fps
        private double _phase;                                     // seconds since start
        private bool _spinning = true;

        public SpinnerBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(64, 64);

            _timer.Tick += (_, _) =>
            {
                _phase += _timer.Interval / 1000.0;
                Invalidate();
            };

            VisibleChanged += (_, _) => SyncTimer();
        }

        // ------------------------------------------------------------------
        // Designer surface
        // ------------------------------------------------------------------

        [Category("Appearance"), DefaultValue(SpinnerLook.Arc)]
        [Description("Which flat spinner pattern to draw.")]
        public SpinnerLook Look { get; set; } = SpinnerLook.Arc;

        [Category("Appearance")]
        [Description("Main stroke colour (the accent).")]
        public Color AccentColor { get; set; } = Color.FromArgb(0, 120, 215);

        [Category("Appearance")]
        [Description("Secondary colour (inner ring / faded tail).")]
        public Color SoftColor { get; set; } = Color.FromArgb(200, 210, 220);

        [Category("Appearance"), DefaultValue(4f)]
        [Description("Stroke thickness in pixels at 96dpi; scales with control size.")]
        public float Thickness { get; set; } = 4f;

        [Category("Behavior"), DefaultValue(1.0)]
        [Description("Speed multiplier. 1 = default pace.")]
        public double Speed { get; set; } = 1.0;

        [Category("Behavior"), DefaultValue(true)]
        [Description("Whether the spinner is animating. False freezes and stops the timer.")]
        public bool Spinning
        {
            get => _spinning;
            set { _spinning = value; SyncTimer(); Invalidate(); }
        }

        private void SyncTimer()
        {
            bool shouldRun = _spinning && Visible && !DesignMode;
            if (shouldRun && !_timer.Enabled) _timer.Start();
            else if (!shouldRun && _timer.Enabled) _timer.Stop();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SyncTimer();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }

        // ------------------------------------------------------------------
        // Drawing
        // ------------------------------------------------------------------

        /// <summary>Cosine ease-in-out over 0..1 - the polish ingredient.</summary>
        private static double Ease(double t) => 0.5 - 0.5 * Math.Cos(Math.PI * 2 * t);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int side = Math.Min(ClientSize.Width, ClientSize.Height);
            if (side < 8) return;

            float stroke = Math.Max(1.5f, Thickness * side / 64f);
            RectangleF bounds = new(
                (ClientSize.Width - side) / 2f + stroke,
                (ClientSize.Height - side) / 2f + stroke,
                side - stroke * 2, side - stroke * 2);

            double t = _phase * Speed;

            switch (Look)
            {
                case SpinnerLook.Arc: DrawArc(g, bounds, stroke, t); break;
                case SpinnerLook.DualRing: DrawDualRing(g, bounds, stroke, t); break;
                case SpinnerLook.Dots: DrawDots(g, bounds, t); break;
                case SpinnerLook.Segments: DrawSegments(g, bounds, stroke, t); break;
                case SpinnerLook.TripleArc: DrawTripleArc(g, bounds, stroke, t); break;
                case SpinnerLook.SquareChase: DrawSquareChase(g, bounds, t); break;
                case SpinnerLook.OrbitEcho: DrawOrbitEcho(g, bounds, stroke, t); break;
                case SpinnerLook.PolygonPulse: DrawPolygonPulse(g, bounds, stroke, t); break;
                case SpinnerLook.EqualizerRing: DrawEqualizerRing(g, bounds, stroke, t); break;
                case SpinnerLook.InfinityChase: DrawInfinityChase(g, bounds, stroke, t); break;
                case SpinnerLook.FlowingDash: DrawFlowingDash(g, bounds, stroke, t); break;
                case SpinnerLook.ConcentricRipples: DrawConcentricRipples(g, bounds, stroke, t); break;
                case SpinnerLook.PieWipe: DrawPieWipe(g, bounds, stroke, t); break;
                case SpinnerLook.Pendulum: DrawPendulum(g, bounds, stroke, t); break;
                case SpinnerLook.NewtonCradle: DrawNewtonCradle(g, bounds, stroke, t); break;
                case SpinnerLook.MorphCycle: DrawMorphCycle(g, bounds, stroke, t); break;
                case SpinnerLook.DnaHelix: DrawDnaHelix(g, bounds, stroke, t); break;
                case SpinnerLook.IrisShutter: DrawIrisShutter(g, bounds, stroke, t); break;
                case SpinnerLook.TripleOrbit: DrawTripleOrbit(g, bounds, stroke, t); break;
            }
        }

        private void DrawArc(Graphics g, RectangleF bounds, float stroke, double t)
        {
            // Constant rotation + a breathing sweep (20..300 degrees) on a 1.4s ease cycle.
            float start = (float)(t * 360 * 0.8 % 360);
            float sweep = 20f + 280f * (float)Ease(t / 1.4 % 1.0);

            using var pen = new Pen(AccentColor, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(pen, bounds, start, sweep);
        }

        private void DrawDualRing(Graphics g, RectangleF bounds, float stroke, double t)
        {
            float outerStart = (float)(t * 360 * 0.9 % 360);
            float outerSweep = 30f + 220f * (float)Ease(t / 1.6 % 1.0);

            RectangleF inner = RectangleF.Inflate(bounds, -stroke * 2.4f, -stroke * 2.4f);
            float innerStart = (float)(360 - t * 360 * 0.65 % 360);   // counter-rotation
            float innerSweep = 40f + 180f * (float)Ease((t + 0.5) / 1.3 % 1.0);

            using var outerPen = new Pen(AccentColor, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var innerPen = new Pen(SoftColor, stroke * 0.75f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(outerPen, bounds, outerStart, outerSweep);
            g.DrawArc(innerPen, inner, innerStart, -innerSweep);
        }

        private void DrawDots(Graphics g, RectangleF bounds, double t)
        {
            const int dotCount = 8;
            float orbit = bounds.Width / 2f;
            float cx = bounds.X + orbit, cy = bounds.Y + bounds.Height / 2f;
            float baseDot = Math.Max(2f, bounds.Width / 11f);

            for (int i = 0; i < dotCount; i++)
            {
                double angle = Math.PI * 2 * (i / (double)dotCount) + t * 1.6;
                // Each dot breathes with a phase offset - the wave chases around the ring.
                double pulse = Ease((t * 0.9 + i / (double)dotCount) % 1.0);
                float r = baseDot * (0.45f + 0.55f * (float)pulse);

                float x = cx + (float)Math.Cos(angle) * (orbit - baseDot);
                float y = cy + (float)Math.Sin(angle) * (orbit - baseDot);

                using var brush = new SolidBrush(BlendToward(SoftColor, AccentColor, pulse));
                g.FillEllipse(brush, x - r, y - r, r * 2, r * 2);
            }
        }

        private void DrawSegments(Graphics g, RectangleF bounds, float stroke, double t)
        {
            const int segments = 12;
            const float gapDegrees = 14f;
            float per = 360f / segments;
            int head = (int)(t * segments * 1.1) % segments;

            using var pen = new Pen(AccentColor, stroke) { StartCap = LineCap.Flat, EndCap = LineCap.Flat };
            for (int i = 0; i < segments; i++)
            {
                // Distance behind the head decides the fade - a flat comet tail.
                int behind = (head - i + segments) % segments;
                double strength = Math.Max(0, 1.0 - behind / (segments * 0.75));

                pen.Color = BlendToward(SoftColor, AccentColor, strength);
                g.DrawArc(pen, bounds, i * per + gapDegrees / 2f - 90f, per - gapDegrees);
            }
        }

        private void DrawTripleArc(Graphics g, RectangleF bounds, float stroke, double t)
        {
            using var pen = new Pen(AccentColor, stroke)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            for (int i = 0; i < 3; i++)
            {
                float inset = i * stroke * 2.05f;
                RectangleF ring = RectangleF.Inflate(bounds, -inset, -inset);
                double pulse = Ease((t / 1.45 + i * 0.22) % 1.0);
                float start = (float)((t * (0.72 + i * 0.11) * 360 + i * 118) % 360);
                float sweep = 35f + 135f * (float)pulse;
                pen.Width = stroke * (1f - i * 0.14f);
                pen.Color = BlendToward(SoftColor, AccentColor, 1.0 - i * 0.28);
                g.DrawArc(pen, ring, start, i == 1 ? -sweep : sweep);
            }
        }

        private void DrawSquareChase(Graphics g, RectangleF bounds, double t)
        {
            const int beads = 16;
            float baseSize = Math.Max(1.5f, bounds.Width / 22f);
            double head = t * 0.9 % 1.0;
            using var brush = new SolidBrush(AccentColor);

            for (int i = 0; i < beads; i++)
            {
                double path = i / (double)beads;
                double distance = (head - path + 1.0) % 1.0;
                double tail = Ease(Math.Max(0, 1.0 - distance * 5.2) * 0.5);
                double pulse = Ease((t / 1.2 + path) % 1.0);
                float size = baseSize * (0.65f + 0.65f * (float)pulse);
                double side = path * 4;
                float x = side < 1 ? bounds.Left + bounds.Width * (float)side :
                          side < 2 ? bounds.Right : side < 3 ? bounds.Right - bounds.Width * (float)(side - 2) : bounds.Left;
                float y = side < 1 ? bounds.Top : side < 2 ? bounds.Top + bounds.Height * (float)(side - 1) :
                          side < 3 ? bounds.Bottom : bounds.Bottom - bounds.Height * (float)(side - 3);
                brush.Color = BlendToward(SoftColor, AccentColor, tail);
                g.FillEllipse(brush, x - size, y - size, size * 2, size * 2);
            }
        }

        private void DrawOrbitEcho(Graphics g, RectangleF bounds, float stroke, double t)
        {
            float bead = Math.Max(stroke, bounds.Width / 12f);
            float radius = bounds.Width / 2f - bead;
            float cx = bounds.X + bounds.Width / 2f, cy = bounds.Y + bounds.Height / 2f;
            double turn = t * 0.78 % 1.0;
            double pulse = Ease(t / 1.25 % 1.0);
            float sizeScale = 0.75f + 0.35f * (float)pulse;
            RectangleF orbit = RectangleF.Inflate(bounds, -bead, -bead);

            using var orbitPen = new Pen(SoftColor, Math.Max(1f, stroke * 0.45f));
            using var brush = new SolidBrush(AccentColor);
            g.DrawEllipse(orbitPen, orbit);

            for (int i = 3; i >= 0; i--)
            {
                double angle = Math.PI * 2 * (turn - i * (0.035 + 0.008 * pulse)) - Math.PI / 2;
                float x = cx + (float)Math.Cos(angle) * radius;
                float y = cy + (float)Math.Sin(angle) * radius;
                float size = bead * sizeScale * (1f - i * 0.17f);
                brush.Color = BlendToward(SoftColor, AccentColor, 1.0 - i / 3.5);
                g.FillEllipse(brush, x - size, y - size, size * 2, size * 2);
            }
        }

        private void DrawPolygonPulse(Graphics g, RectangleF bounds, float stroke, double t)
        {
            const int vertices = 6;
            float cx = bounds.X + bounds.Width / 2f, cy = bounds.Y + bounds.Height / 2f;
            float radius = bounds.Width / 2f;
            double pulse = Ease(t / 1.35 % 1.0);
            double rotation = t * 0.62 % 1.0 * Math.PI * 2;
            var outer = new PointF[vertices];
            var inner = new PointF[vertices];

            for (int i = 0; i < vertices; i++)
            {
                double angle = rotation + i * Math.PI * 2 / vertices - Math.PI / 2;
                float flex = (float)(i % 2 == 0 ? 0.72 + pulse * 0.28 : 1.0 - pulse * 0.22);
                outer[i] = new PointF(cx + (float)Math.Cos(angle) * radius * flex,
                                      cy + (float)Math.Sin(angle) * radius * flex);
                inner[i] = new PointF(cx + (outer[i].X - cx) * 0.48f,
                                      cy + (outer[i].Y - cy) * 0.48f);
            }

            using var outerPen = new Pen(AccentColor, stroke) { LineJoin = LineJoin.Round };
            using var innerPen = new Pen(BlendToward(SoftColor, AccentColor, pulse), stroke * 0.65f) { LineJoin = LineJoin.Round };
            g.DrawPolygon(outerPen, outer);
            g.DrawPolygon(innerPen, inner);
        }

        private void DrawEqualizerRing(Graphics g, RectangleF bounds, float stroke, double t)
        {
            const int bars = 14;
            float cx = bounds.X + bounds.Width / 2f, cy = bounds.Y + bounds.Height / 2f;
            float radius = bounds.Width / 2f;
            double rotation = t * 0.16 % 1.0 * Math.PI * 2;
            using var pen = new Pen(AccentColor, stroke * 0.78f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            for (int i = 0; i < bars; i++)
            {
                double wave = Ease((t / 1.1 + i / (double)bars) % 1.0);
                double angle = rotation + i * Math.PI * 2 / bars - Math.PI / 2;
                float inner = radius * (0.38f + 0.08f * (float)wave);
                float outer = radius * (0.68f + 0.32f * (float)wave);
                float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
                pen.Color = BlendToward(SoftColor, AccentColor, 0.3 + wave * 0.7);
                g.DrawLine(pen, cx + cos * inner, cy + sin * inner,
                                cx + cos * outer, cy + sin * outer);
            }
        }

        private void DrawInfinityChase(Graphics g, RectangleF bounds, float stroke, double t)
        {
            float cx = bounds.X + bounds.Width / 2f, cy = bounds.Y + bounds.Height / 2f;
            float a = bounds.Width * 0.45f;

            // Lemniscate of Bernoulli, sampled into a polyline.
            // (Guest submission had path.GetLastPoint() on an empty GraphicsPath here -
            // that throws on the first segment, so the points are collected instead.)
            var points = new PointF[61];
            for (int i = 0; i <= 60; i++)
            {
                double aRad = Math.PI * 2 * (i / 60.0);
                double denom = 1 + Math.Sin(aRad) * Math.Sin(aRad);
                points[i] = new PointF(
                    cx + (float)(a * Math.Cos(aRad) / denom),
                    cy + (float)(a * Math.Sin(aRad) * Math.Cos(aRad) / denom));
            }

            using var pen = new Pen(SoftColor, stroke * 0.5f);
            g.DrawLines(pen, points);

            double pulse = Ease(t / 1.1 % 1.0);
            float r = Math.Max(2f, stroke * (0.8f + 0.4f * (float)pulse));
            using var brush = new SolidBrush(AccentColor);
            for (int k = 0; k < 2; k++)
            {
                double angle = Math.PI * 2 * (t * 0.6 + k * 0.5) % (Math.PI * 2);
                double d = 1 + Math.Sin(angle) * Math.Sin(angle);
                float bx = cx + (float)(a * Math.Cos(angle) / d);
                float by = cy + (float)(a * Math.Sin(angle) * Math.Cos(angle) / d);
                brush.Color = BlendToward(SoftColor, AccentColor, k == 0 ? pulse : 1.0 - pulse);
                g.FillEllipse(brush, bx - r, by - r, r * 2, r * 2);
            }
        }

        private void DrawFlowingDash(Graphics g, RectangleF bounds, float stroke, double t)
        {
            double pulse = Ease(t / 1.3 % 1.0);
            float dash = 2f + 3f * (float)pulse;
            float space = 6f - 3f * (float)pulse;
            using var pen = new Pen(AccentColor, stroke)
            {
                DashStyle = DashStyle.Custom,
                DashPattern = new float[] { dash, space },
                DashOffset = (float)(t * 12.0)
            };
            g.DrawEllipse(pen, bounds);

            RectangleF inner = RectangleF.Inflate(bounds, -stroke * 2.2f, -stroke * 2.2f);
            using var innerPen = new Pen(BlendToward(SoftColor, AccentColor, pulse), stroke * 0.75f)
            {
                DashStyle = DashStyle.Custom,
                DashPattern = new float[] { space, dash },
                DashOffset = (float)(-t * 10.0)
            };
            g.DrawEllipse(innerPen, inner);
        }

        private void DrawConcentricRipples(Graphics g, RectangleF bounds, float stroke, double t)
        {
            const int rings = 4;
            float maxR = bounds.Width / 2f;
            float cx = bounds.X + maxR, cy = bounds.Y + bounds.Height / 2f;
            using var pen = new Pen(AccentColor, stroke);

            for (int i = 0; i < rings; i++)
            {
                double progress = (t * 0.7 + i / (double)rings) % 1.0;
                double easedProg = Ease(progress);
                float currentR = maxR * (float)progress;
                if (currentR < 1f) continue;

                double opacity = Math.Sin(progress * Math.PI);
                pen.Color = BlendToward(SoftColor, AccentColor, opacity * easedProg);
                pen.Width = Math.Max(1f, stroke * (float)(1.0 - progress * 0.4));
                g.DrawEllipse(pen, cx - currentR, cy - currentR, currentR * 2, currentR * 2);
            }
        }

        private void DrawPieWipe(Graphics g, RectangleF bounds, float stroke, double t)
        {
            double pulse = Ease(t / 1.2 % 1.0);
            float startAngle = (float)(t * 220 % 360);
            float sweepAngle = 20f + 320f * (float)pulse;

            RectangleF track = RectangleF.Inflate(bounds, -stroke * 0.5f, -stroke * 0.5f);
            using var trackPen = new Pen(BlendToward(SoftColor, AccentColor, 0.2), stroke * 0.5f);
            g.DrawEllipse(trackPen, track);

            using var brush = new SolidBrush(BlendToward(SoftColor, AccentColor, pulse));
            g.FillPie(brush, bounds.X, bounds.Y, bounds.Width, bounds.Height, startAngle, sweepAngle);
        }

        private void DrawPendulum(Graphics g, RectangleF bounds, float stroke, double t)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float pivotY = bounds.Y + stroke * 0.6f;
            float length = bounds.Height * 0.62f;
            float bobR = Math.Max(stroke * 0.9f, bounds.Width / 11f);

            // Swing amplitude derived from the room that actually exists: the bob must stay
            // inside the bounds at full extension. (Guest submission hardcoded 68 degrees,
            // which clipped the bob off both sides of the control.)
            float room = bounds.Width / 2f - bobR;
            float maxRad = (float)Math.Asin(Math.Clamp(room / length, 0.2f, 0.85f));
            float maxDeg = maxRad * 180f / (float)Math.PI;
            float angle = (float)(Ease(t / 1.55 % 1.0) * 2.0 - 1.0) * maxRad;
            float bx = cx + (float)Math.Sin(angle) * length;
            float by = pivotY + (float)Math.Cos(angle) * length;
            float echoA = (float)(Ease((t / 1.55 - 0.07 + 1.0) % 1.0) * 2.0 - 1.0) * maxRad;

            using var arcPen = new Pen(SoftColor, Math.Max(1f, stroke * 0.4f));
            g.DrawArc(arcPen, cx - length, pivotY - length, length * 2f, length * 2f,
                      90f - maxDeg, maxDeg * 2f);

            using var rod = new Pen(AccentColor, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(rod, cx, pivotY, bx, by);

            using var echo = new SolidBrush(BlendToward(SoftColor, AccentColor, 0.32));
            float er = bobR * 0.52f;
            g.FillEllipse(echo,
                cx + (float)Math.Sin(echoA) * length - er,
                pivotY + (float)Math.Cos(echoA) * length - er, er * 2, er * 2);

            using var bob = new SolidBrush(AccentColor);
            g.FillEllipse(bob, bx - bobR, by - bobR, bobR * 2, bobR * 2);
            using var pivot = new SolidBrush(SoftColor);
            g.FillEllipse(pivot, cx - stroke * 0.7f, pivotY - stroke * 0.7f, stroke * 1.4f, stroke * 1.4f);
        }

        private void DrawNewtonCradle(Graphics g, RectangleF bounds, float stroke, double t)
        {
            const int n = 5;
            float pivotY = bounds.Y + bounds.Height * 0.12f;
            float r = Math.Max(1.6f, bounds.Width / (n * 2.9f));
            float stringLen = bounds.Height * 0.62f;
            float cx = bounds.X + bounds.Width / 2f;
            double cycle = t / 1.2 % 1.0;
            double left = cycle < 0.5 ? -Ease(cycle * 2.0) : 0;
            double right = cycle >= 0.5 ? Ease((cycle - 0.5) * 2.0) : 0;

            float firstX = cx - (n - 1) * r;

            // Throw angle sized to the space the end ball actually has before the control
            // edge - the submitted fixed 40-degree throw swung the ball clean out of bounds.
            float room = firstX - bounds.Left - r;
            double throwRad = Math.Asin(Math.Clamp(room / stringLen, 0.08f, 0.75f));

            using var line = new Pen(SoftColor, Math.Max(1f, stroke * 0.4f));
            using var brush = new SolidBrush(AccentColor);
            g.DrawLine(line, firstX, pivotY, firstX + (n - 1) * r * 2f, pivotY);

            for (int i = 0; i < n; i++)
            {
                float restX = cx + (i - (n - 1) / 2f) * r * 2f;
                double ang = i == 0 ? left * throwRad : i == n - 1 ? right * throwRad : 0;
                float bx = restX + (float)Math.Sin(ang) * stringLen;
                float by = pivotY + (float)Math.Cos(ang) * stringLen;
                g.DrawLine(line, restX, pivotY, bx, by);
                brush.Color = BlendToward(SoftColor, AccentColor, i == 0 || i == n - 1 ? 1.0 : 0.55);
                g.FillEllipse(brush, bx - r, by - r, r * 2, r * 2);
            }
        }

        private void DrawMorphCycle(Graphics g, RectangleF bounds, float stroke, double t)
        {
            const int samples = 48;
            float cx = bounds.X + bounds.Width / 2f, cy = bounds.Y + bounds.Height / 2f;
            float R = bounds.Width / 2f;
            double cycle = t / 2.2 % 3.0;
            int stage = (int)cycle;

            // HALF the Ease cycle (0..0.5) so the morph runs 0 -> 1 monotonically. The
            // submitted full-cycle Ease morphed out and BACK each stage, then hard-jumped
            // to the next shape - a visible pop three times per cycle.
            float morph = (float)Ease((cycle - stage) * 0.5);

            int n0 = stage == 0 ? 32 : stage == 1 ? 3 : 4;
            int n1 = stage == 0 ? 3 : stage == 1 ? 4 : 32;
            double rot = t * 0.28 * Math.PI * 2;
            var pts = new PointF[samples];

            for (int i = 0; i < samples; i++)
            {
                double local = Math.PI * 2 * i / samples;
                double world = local + rot - Math.PI / 2;
                float r0 = PolyRadius(R, n0, local + (n0 % 2 == 0 ? Math.PI / n0 : 0));
                float r1 = PolyRadius(R, n1, local + (n1 % 2 == 0 ? Math.PI / n1 : 0));
                float r = r0 + (r1 - r0) * morph;
                pts[i] = new PointF(cx + (float)Math.Cos(world) * r, cy + (float)Math.Sin(world) * r);
            }

            using var pen = new Pen(AccentColor, stroke) { LineJoin = LineJoin.Round };
            g.DrawPolygon(pen, pts);
            double pulse = Ease(t / 1.25 % 1.0);
            float core = R * (0.16f + 0.10f * (float)pulse);
            using var inner = new Pen(BlendToward(SoftColor, AccentColor, pulse), stroke * 0.65f);
            g.DrawEllipse(inner, cx - core, cy - core, core * 2, core * 2);
        }

        private void DrawDnaHelix(Graphics g, RectangleF bounds, float stroke, double t)
        {
            const int rungs = 7;
            float cx = bounds.X + bounds.Width / 2f;
            float amp = bounds.Width * (0.26f + 0.08f * (float)Ease(t / 1.45 % 1.0));
            float y0 = bounds.Y + stroke;
            float span = bounds.Height - stroke * 2f;
            using var rungPen = new Pen(SoftColor, Math.Max(1f, stroke * 0.45f));
            using var aBrush = new SolidBrush(AccentColor);
            using var bBrush = new SolidBrush(SoftColor);

            for (int i = 0; i < rungs; i++)
            {
                double phase = t * 1.35 + i * 0.48;
                float y = y0 + span * i / (rungs - 1f);
                float x1 = cx + amp * (float)Math.Sin(phase);
                float x2 = cx - amp * (float)Math.Sin(phase);
                double vis = Ease((t / 1.55 + i / (double)rungs) % 1.0);
                rungPen.Color = BlendToward(SoftColor, AccentColor, 0.25 + vis * 0.5);
                g.DrawLine(rungPen, x1, y, x2, y);
                double front = 0.5 + 0.5 * Math.Sin(phase);
                float d1 = Math.Max(1.5f, stroke * (0.55f + 0.55f * (float)front));
                float d2 = Math.Max(1.5f, stroke * (0.55f + 0.55f * (float)(1.0 - front)));
                aBrush.Color = BlendToward(SoftColor, AccentColor, front);
                bBrush.Color = BlendToward(SoftColor, AccentColor, 1.0 - front);
                g.FillEllipse(aBrush, x1 - d1, y - d1, d1 * 2, d1 * 2);
                g.FillEllipse(bBrush, x2 - d2, y - d2, d2 * 2, d2 * 2);
            }
        }

        private void DrawIrisShutter(Graphics g, RectangleF bounds, float stroke, double t)
        {
            const int blades = 6;
            float cx = bounds.X + bounds.Width / 2f, cy = bounds.Y + bounds.Height / 2f;
            float R = bounds.Width / 2f;
            double open = Ease(t / 1.55 % 1.0);
            float aperture = R * (0.14f + 0.30f * (float)open);
            double rot = t * 0.38 * Math.PI * 2;
            using var ring = new Pen(SoftColor, Math.Max(1f, stroke * 0.42f));
            g.DrawEllipse(ring, bounds);

            // Outline-only blades - the filled wedges read heavy against the flat icon
            // language (David: "too much fill"). The accent breathes through the stroke
            // colour as the aperture opens.
            using var edge = new Pen(BlendToward(SoftColor, AccentColor, 0.45 + open * 0.55),
                Math.Max(1f, stroke * 0.6f)) { LineJoin = LineJoin.Round };
            var tri = new PointF[3];
            for (int i = 0; i < blades; i++)
            {
                double a0 = rot + i * Math.PI * 2 / blades;
                double a1 = a0 + Math.PI * 2 / blades * 0.94;
                double mid = (a0 + a1) * 0.5;
                tri[0] = new PointF(cx + (float)Math.Cos(a0) * R, cy + (float)Math.Sin(a0) * R);
                tri[1] = new PointF(cx + (float)Math.Cos(a1) * R, cy + (float)Math.Sin(a1) * R);
                tri[2] = new PointF(cx + (float)Math.Cos(mid) * aperture, cy + (float)Math.Sin(mid) * aperture);
                g.DrawPolygon(edge, tri);
            }
        }

        private void DrawTripleOrbit(Graphics g, RectangleF bounds, float stroke, double t)
        {
            // TripleArc drawn as orbs: three nested rings, all rotating the SAME direction
            // at TripleArc's slightly different speeds, each trail a tight dotted arc whose
            // LENGTH breathes the way TripleArc's sweeps do (short..long on the stagger).
            const int echoes = 5;   // lead + 5 ghosts
            float cx = bounds.X + bounds.Width / 2f, cy = bounds.Y + bounds.Height / 2f;
            float R = bounds.Width / 2f;
            float baseBead = Math.Max(2f, bounds.Width / 14f);

            using var brush = new SolidBrush(AccentColor);
            for (int ring = 0; ring < 3; ring++)
            {
                float orbit = R * (1f - ring * 0.30f) - baseBead;
                float bead = baseBead * (1f - ring * 0.18f);
                double pulse = Ease((t / 1.45 + ring * 0.22) % 1.0);   // TripleArc's stagger
                double lead = t * (1.44 + ring * 0.22) * Math.PI * 2 + ring * 2.1;

                // Trail-length breathing: tight orbs whose spacing stretches with the pulse -
                // the whole trail spans ~40deg when short, ~130deg at full breath.
                double spacing = 0.020 + 0.040 * pulse;

                for (int i = echoes; i >= 0; i--)
                {
                    double angle = lead - i * spacing * Math.PI * 2;
                    float size = bead * (1f - i * 0.13f);
                    float x = cx + (float)Math.Cos(angle) * orbit;
                    float y = cy + (float)Math.Sin(angle) * orbit;

                    // Ring depth AND trail position both fade toward SoftColor.
                    double strength = (1.0 - ring * 0.22) * (1.0 - i / (echoes + 1.5));
                    brush.Color = BlendToward(SoftColor, AccentColor, strength);
                    g.FillEllipse(brush, x - size, y - size, size * 2, size * 2);
                }
            }
        }

        /// <summary>Polar radius of a regular n-gon with circumradius R, vertex at local 0.</summary>
        private static float PolyRadius(float R, int n, double localTheta)
        {
            double slice = Math.PI * 2 / n;
            double a = (localTheta % slice + slice) % slice;
            return R * (float)(Math.Cos(Math.PI / n) / Math.Cos(a - Math.PI / n));
        }

        private static Color BlendToward(Color from, Color to, double amount)
        {
            amount = Math.Clamp(amount, 0, 1);
            return Color.FromArgb(
                (int)(from.R + (to.R - from.R) * amount),
                (int)(from.G + (to.G - from.G) * amount),
                (int)(from.B + (to.B - from.B) * amount));
        }
    }
}
