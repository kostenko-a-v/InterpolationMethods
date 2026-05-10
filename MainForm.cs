using System.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace InterpolationApp
{
    public class MainForm : Form
    {

        private static readonly Color BgMain = Color.FromArgb(245, 247, 250);
        private static readonly Color BgPanel = Color.FromArgb(235, 238, 245);
        private static readonly Color BgInput = Color.FromArgb(255, 255, 255);
        private static readonly Color Accent = Color.FromArgb(40, 100, 220);
        private static readonly Color TextMain = Color.FromArgb(30, 34, 40);
        private static readonly Color TextDim = Color.FromArgb(100, 110, 120);
        private static readonly Color CLagrange = Color.FromArgb(0, 120, 215);
        private static readonly Color CAitken = Color.FromArgb(0, 120, 215);
        private static readonly Color CNode = Color.FromArgb(30, 160, 50);
        private static readonly Color CTarget = Color.FromArgb(220, 40, 80);
        private static readonly Color CGrid = Color.FromArgb(50, 160, 180, 200);

        private InterpolationResult resLag, resAit;

        private double zoom = 1.0;
        private float panX = 0;
        private float panY = 0;
        private Point lastMousePos;
        private bool isPanning = false;

        public MainForm()
        {
            InitializeComponent();
            pnlPoly.Resize += (s, e) => pnlPoly.Invalidate();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            dgv.Rows.Add("0", "0");
            dgv.Rows.Add("0", "0");
            cmbMethod.SelectedIndex = 0;
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            if (dgv.Rows.Count >= DataValidator.MaxNodes)
            {
                ShowMsg($"Максимальна кількість вузлів — {DataValidator.MaxNodes}.", true);
                return;
            }
            dgv.Rows.Add("", "");
        }

        private void PnlChart_Resize(object sender, EventArgs e)
        {
            pnlChart.Invalidate();
        }

        private void Dgv_CellValidated(object sender, DataGridViewCellEventArgs e)
        {
            var cell = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex];
            string val = cell.Value?.ToString()?.Trim();
            string err = DataValidator.ValidateCellValue(val);
            cell.ErrorText = err;

            // Якщо помилок немає і це число — округлюємо відразу після введення
            if (string.IsNullOrEmpty(err) && !string.IsNullOrEmpty(val))
            {
                if (DataValidator.TryParseNumber(val, out double num))
                {
                    double rounded = Math.Round(num, DataValidator.MaxDecimalPlaces, MidpointRounding.AwayFromZero);
                    cell.Value = rounded.ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        private void TxtX_Validated(object sender, EventArgs e)
        {
            string val = txtX.Text.Trim();
            string err = DataValidator.ValidateCellValue(val);
            errProv.SetError(txtX, err);

            if (string.IsNullOrEmpty(err) && !string.IsNullOrEmpty(val))
            {
                if (DataValidator.TryParseNumber(val, out double num))
                {
                    double rounded = Math.Round(num, DataValidator.MaxDecimalPlaces, MidpointRounding.AwayFromZero);
                    txtX.Text = rounded.ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        private void BtnDel_Click(object sender, EventArgs e)
        {
            if (dgv.CurrentRow == null) return;
            if (dgv.Rows.Count <= 2)
            {
                ShowMsg("Не можна видалити рядок: потрібно щонайменше 2 вузли.", true);
                return;
            }
            dgv.Rows.RemoveAt(dgv.CurrentRow.Index);
        }

        private void BtnLoad_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Завантажити вузли з файлу",
                Filter = "CSV / текст|*.csv;*.txt|Усі файли|*.*"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                var nodes = FileManager.LoadNodes(dlg.FileName);
                dgv.Rows.Clear();
                foreach (var n in nodes)
                    dgv.Rows.Add(n.X.ToString(CultureInfo.InvariantCulture),
                                 n.Y.ToString(CultureInfo.InvariantCulture));
                ShowMsg($"Завантажено {nodes.Count} вузлів з файлу.", false);
            }
            catch (Exception ex)
            {
                ShowMsg(ex.Message, true);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (resLag == null && resAit == null)
            {
                ShowMsg("Спочатку обчисліть результати.", true);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Зберегти результати інтерполяції",
                Filter = "Текстовий файл (*.txt)|*.txt|Усі файли (*.*)|*.*",
                DefaultExt = "txt",
                FileName = "InterpolationResult.txt"
            };

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    bool appended = false;
                    if (resLag != null)
                    {
                        FileManager.SaveResult(resLag, dlg.FileName, false);
                        appended = true;
                    }

                    if (resAit != null)
                    {
                        FileManager.SaveResult(resAit, dlg.FileName, appended);
                    }

                    ShowMsg("Результати успішно збережено.", false);
                }
                catch (Exception ex)
                {
                    ShowMsg("Помилка збереження: " + ex.Message, true);
                }
            }
        }

        private void BtnCalc_Click(object sender, EventArgs e)
        {
            resLag = null; resAit = null;
            pnlPoly.Controls.Clear();
            try
            {
                dgv.EndEdit();

                var rawRows = new List<(string sx, string sy)>();
                for (int i = 0; i < dgv.Rows.Count; i++)
                {
                    string sx = dgv.Rows[i].Cells[0].Value?.ToString();
                    string sy = dgv.Rows[i].Cells[1].Value?.ToString();
                    rawRows.Add((sx, sy));
                }

                var (nodes, nodesError) = DataValidator.ValidateAndParseNodes(rawRows);
                if (nodesError != null)
                { ShowMsg(nodesError, true); pnlPoly.Invalidate(); pnlChart.Invalidate(); return; }

                // Оновлюємо таблицю округленими значеннями
                int nodeIdx = 0;
                for (int i = 0; i < dgv.Rows.Count; i++)
                {
                    string sx = dgv.Rows[i].Cells[0].Value?.ToString();
                    string sy = dgv.Rows[i].Cells[1].Value?.ToString();
                    if (string.IsNullOrWhiteSpace(sx) && string.IsNullOrWhiteSpace(sy)) continue;
                    dgv.Rows[i].Cells[0].Value = nodes[nodeIdx].X.ToString(CultureInfo.InvariantCulture);
                    dgv.Rows[i].Cells[1].Value = nodes[nodeIdx].Y.ToString(CultureInfo.InvariantCulture);
                    nodeIdx++;
                }

                var (targetX, xError) = DataValidator.ValidateTargetX(txtX.Text);
                if (xError != null)
                { ShowMsg(xError, true); pnlPoly.Invalidate(); pnlChart.Invalidate(); return; }
                
                txtX.Text = targetX.ToString(CultureInfo.InvariantCulture);

                string method = cmbMethod.SelectedItem.ToString();
                if (method == "Лагранж")
                    resLag = LagrangeInterpolation.Interpolate(nodes, targetX, 5000);
                if (method == "Ейткен")
                    resAit = AitkenInterpolation.Interpolate(nodes, targetX, 5000);

                if (resLag != null && DataValidator.IsResultOverflow(resLag.InterpolatedValue))
                {
                    resLag = null;
                    ShowMsg("Переповнення при обчисленні методом Лагранжа.", true);
                    pnlPoly.Invalidate(); pnlChart.Invalidate(); return;
                }
                if (resAit != null && DataValidator.IsResultOverflow(resAit.InterpolatedValue))
                {
                    resAit = null;
                    ShowMsg("Переповнення при обчисленні схемою Ейткена.", true);
                    pnlPoly.Invalidate(); pnlChart.Invalidate(); return;
                }

                ShowMsg("Обчислено успішно", false);

                zoom = 1.0;
                panX = 0;
                panY = 0;
            }
            catch (Exception ex)
            {
                ShowMsg(ex.Message, true);
            }
            UpdatePolyControls();
            pnlPoly.Invalidate(); pnlChart.Invalidate();
        }

        private void UpdatePolyControls()
        {
            pnlPoly.Controls.Clear();
            if (resLag == null && resAit == null) return;

            int py = 8;
            var list = new List<(string n, InterpolationResult r, Color c)>();
            if (resLag != null) list.Add(("Метод Лагранжа", resLag, CLagrange));
            if (resAit != null) list.Add(("Схема Ейткена", resAit, CAitken));

            foreach (var (nm, r, cl) in list)
            {
                var lblTitle = MkLabel(nm, 16, py, 200, 18, 10f, FontStyle.Regular, cl);
                lblTitle.Font = new Font("Segoe UI Semibold", 9.5f);
                pnlPoly.Controls.Add(lblTitle);
                py += 19;

                var txtPoly = new TextBox
                {
                    ReadOnly = true,
                    BackColor = pnlPoly.BackColor,
                    ForeColor = TextMain,
                    BorderStyle = BorderStyle.None,
                    Font = new Font("Consolas", 11f),
                    Location = new Point(24, py),
                    Size = new Size(pnlPoly.Width - 40, 42),
                    Multiline = true,
                    WordWrap = false,
                    ScrollBars = ScrollBars.Horizontal,
                    Text = "P(x) = " + r.Polynomial.ToUnicodeString()
                };
                pnlPoly.Controls.Add(txtPoly);
                py += 44;

                string info = $"P({r.TargetX:G7}) = {r.InterpolatedValue:G7}   │   Опер.: {r.OperationCount}   │   {r.TheoreticalComplexity}";
                var lblInfo = MkLabel(info, 24, py, pnlPoly.Width - 40, 18, 8.5f, FontStyle.Regular, TextDim);
                pnlPoly.Controls.Add(lblInfo);
                py += 25;
            }
        }

        private Label MkLabel(string t, int x, int y, int w, int h, float sz, FontStyle st, Color c)
            => new Label
            {
                Text = t,
                Location = new Point(x, y),
                Size = new Size(w, h),
                ForeColor = c,
                Font = new Font("Segoe UI", sz, st),
                BackColor = Color.Transparent
            };

        private void ShowMsg(string msg, bool isError)
        {
            lblStatus.ForeColor = isError ? Color.FromArgb(220, 40, 40) : Color.FromArgb(30, 140, 60);
            lblStatus.Text = msg;
        }

        private void PnlPoly_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var bp = new Pen(Color.FromArgb(50, Accent), 1f);
            g.DrawRectangle(bp, 0, 0, pnlPoly.Width - 1, pnlPoly.Height - 1);

            if (resLag == null && resAit == null)
            {
                using var f = new Font("Segoe UI", 11f, FontStyle.Italic);
                g.DrawString("Введіть дані та натисніть «Обчислити»", f, new SolidBrush(TextDim), 20, pnlPoly.Height / 2 - 12);
            }
        }

        private void PnlChart_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (var bg = new LinearGradientBrush(pnlChart.ClientRectangle, BgMain, Color.FromArgb(235, 240, 245), 90f))
                g.FillRectangle(bg, pnlChart.ClientRectangle);

            if (resLag == null && resAit == null)
            {
                using var f = new Font("Segoe UI", 12f, FontStyle.Italic);
                var sz = g.MeasureString("Графік з'явиться після обчислення", f);
                g.DrawString("Графік з'явиться після обчислення", f, new SolidBrush(TextDim),
                    (pnlChart.Width - sz.Width) / 2, (pnlChart.Height - sz.Height) / 2);
                return;
            }

            const int ML = 70, MR = 25, MT = 35, MB = 50;
            var plot = new Rectangle(ML, MT, pnlChart.Width - ML - MR, pnlChart.Height - MT - MB);
            if (plot.Width < 40 || plot.Height < 40) return;

            GetBounds(out double xMin, out double xMax, out double yMin, out double yMax);

            double cx = (xMin + xMax) / 2.0;
            double cy = (yMin + yMax) / 2.0;

            double baseScaleX = (double)plot.Width / (xMax - xMin);
            double baseScaleY = (double)plot.Height / (yMax - yMin);
            double baseScale = Math.Min(baseScaleX, baseScaleY);
            double scale = baseScale * zoom;

            Func<double, float> tx = x => (float)(plot.Left + plot.Width / 2.0 + (x - cx) * scale + panX);
            Func<double, float> ty = y => (float)(plot.Top + plot.Height / 2.0 - (y - cy) * scale + panY);

            Func<float, double> fx = sx => cx + (sx - plot.Left - plot.Width / 2.0 - panX) / scale;
            Func<float, double> fy = sy => cy - (sy - plot.Top - plot.Height / 2.0 - panY) / scale;

            double vXMin = fx(plot.Left);
            double vXMax = fx(plot.Right);
            double vYMin = fy(plot.Bottom);
            double vYMax = fy(plot.Top);

            DrawGrid(g, plot, vXMin, vXMax, vYMin, vYMax, tx, ty);

            g.SetClip(plot);

            if (resLag != null) DrawCurve(g, resLag, CLagrange, DashStyle.Solid, tx, ty, plot, vXMin, vXMax, vYMin, vYMax);
            if (resAit != null) DrawCurve(g, resAit, CAitken, resLag != null ? DashStyle.Dash : DashStyle.Solid, tx, ty, plot, vXMin, vXMax, vYMin, vYMax);
            DrawNodes(g, (resLag ?? resAit).InputNodes, tx, ty, plot);
            if (resLag != null) DrawTarget(g, resLag, tx, ty, plot);
            else if (resAit != null) DrawTarget(g, resAit, tx, ty, plot);

            g.ResetClip();

            using var tf = new Font("Segoe UI", 12f, FontStyle.Bold);
            var tsz = g.MeasureString("Графік інтерполяційного полінома", tf);
            g.DrawString("Графік інтерполяційного полінома", tf, new SolidBrush(TextMain),
                (pnlChart.Width - tsz.Width) / 2, 8);

            using var zf = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            string zoomText = $"Масштаб: {(zoom * 100):F0}%";
            var zsz = g.MeasureString(zoomText, zf);
            g.DrawString(zoomText, zf, new SolidBrush(TextDim),
                10, pnlChart.Height - zsz.Height - 10);
        }

        private void GetBounds(out double xMin, out double xMax, out double yMin, out double yMax)
        {
            xMin = -DataValidator.MaxAbsValue;
            xMax = DataValidator.MaxAbsValue;
            yMin = -DataValidator.MaxAbsValue;
            yMax = DataValidator.MaxAbsValue;
        }

        private void ClampPan()
        {
            const int ML = 70, MR = 25, MT = 35, MB = 50;
            var plot = new Rectangle(ML, MT, pnlChart.Width - ML - MR, pnlChart.Height - MT - MB);
            if (plot.Width < 40 || plot.Height < 40) return;

            GetBounds(out double xMin, out double xMax, out double yMin, out double yMax);
            double rangeX = xMax - xMin;
            double rangeY = yMax - yMin;

            double baseScaleX = (double)plot.Width / rangeX;
            double baseScaleY = (double)plot.Height / rangeY;
            double baseScale = Math.Min(baseScaleX, baseScaleY);
            double scale = baseScale * zoom;

            float limitX = (float)Math.Max(0, rangeX / 2.0 * scale - plot.Width / 2.0);
            double worldYRange = 1000000.0;
            float limitY = (float)Math.Max(0, worldYRange * scale - plot.Height / 2.0);

            panX = Math.Max(-limitX, Math.Min(limitX, panX));
            panY = Math.Max(-limitY, Math.Min(limitY, panY));
        }

        private void PnlChart_MouseWheel(object sender, MouseEventArgs e)
        {
            if (resLag == null && resAit == null) return;

            const int ML = 70, MR = 25, MT = 35, MB = 50;
            var plot = new Rectangle(ML, MT, pnlChart.Width - ML - MR, pnlChart.Height - MT - MB);
            if (plot.Width < 40 || plot.Height < 40) return;

            GetBounds(out double xMin, out double xMax, out double yMin, out double yMax);
            double cx = (xMin + xMax) / 2.0;
            double cy = (yMin + yMax) / 2.0;

            double baseScaleX = (double)plot.Width / (xMax - xMin);
            double baseScaleY = (double)plot.Height / (yMax - yMin);
            double baseScale = Math.Min(baseScaleX, baseScaleY);

            double oldScale = baseScale * zoom;
            double worldX = cx + (e.X - plot.Left - plot.Width / 2.0 - panX) / oldScale;
            double worldY = cy - (e.Y - plot.Top - plot.Height / 2.0 - panY) / oldScale;

            if (e.Delta > 0) zoom *= 1.25;
            else zoom /= 1.25;

            if (zoom < 1.0) zoom = 1.0;
            if (zoom > 2e5) zoom = 2e5;

            double newScale = baseScale * zoom;
            panX = (float)(e.X - (plot.Left + plot.Width / 2.0 + (worldX - cx) * newScale));
            panY = (float)(e.Y - (plot.Top + plot.Height / 2.0 - (worldY - cy) * newScale));

            ClampPan();
            pnlChart.Invalidate();
        }

        private void PnlChart_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isPanning = true;
                lastMousePos = e.Location;
            }
        }

        private void PnlChart_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPanning)
            {
                panX += (e.X - lastMousePos.X);
                panY += (e.Y - lastMousePos.Y);
                lastMousePos = e.Location;
                ClampPan();
                pnlChart.Invalidate();
            }
        }

        private void PnlChart_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) isPanning = false;
        }

        private void DrawGrid(Graphics g, Rectangle a, double xMin, double xMax,
            double yMin, double yMax, Func<double, float> tx, Func<double, float> ty)
        {
            using var gp = new Pen(CGrid, 1f) { DashStyle = DashStyle.Dot };
            using var ap = new Pen(Color.FromArgb(180, 190, 200), 1.2f);
            using var ft = new Font("Consolas", 8.5f);
            using var br = new SolidBrush(TextDim);

            double xS = NiceStep(xMax - xMin, Math.Max(5, Math.Min(12, a.Width / 90)));
            for (double x = Math.Ceiling(xMin / xS) * xS; x <= xMax + xS * 0.01; x += xS)
            {
                float px = tx(x); if (px < a.Left || px > a.Right) continue;
                g.DrawLine(gp, px, a.Top, px, a.Bottom);
                var s = g.MeasureString(Fmt(x), ft);
                g.DrawString(Fmt(x), ft, br, px - s.Width / 2, a.Bottom + 6);
            }
            double yS = NiceStep(yMax - yMin, Math.Max(4, Math.Min(10, a.Height / 60)));
            for (double y = Math.Ceiling(yMin / yS) * yS; y <= yMax + yS * 0.01; y += yS)
            {
                float py = ty(y); if (py < a.Top || py > a.Bottom) continue;
                g.DrawLine(gp, a.Left, py, a.Right, py);
                var s = g.MeasureString(Fmt(y), ft);
                g.DrawString(Fmt(y), ft, br, a.Left - s.Width - 6, py - s.Height / 2);
            }
            if (xMin <= 0 && xMax >= 0)
            { using var z = new Pen(Color.FromArgb(100, 40, 60, 80), 1.5f); g.DrawLine(z, tx(0), a.Top, tx(0), a.Bottom); }
            if (yMin <= 0 && yMax >= 0)
            { using var z = new Pen(Color.FromArgb(100, 40, 60, 80), 1.5f); g.DrawLine(z, a.Left, ty(0), a.Right, ty(0)); }
            g.DrawRectangle(ap, a);
        }

        private void DrawCurve(Graphics g, InterpolationResult res, Color c, DashStyle ds,
            Func<double, float> tx, Func<double, float> ty, Rectangle clip, double vXMin, double vXMax, double vYMin, double vYMax)
        {
            if (res == null || res.Polynomial == null) return;

            double span = vXMax - vXMin;
            double startX = vXMin - span * 0.1;
            double endX = vXMax + span * 0.1;

            // Збираємо X-координати для обчислення: рівномірна сітка + густо біля вузлів
            var xSet = new SortedSet<double>();

            // 1) Рівномірна сітка
            int uniformCount = Math.Max(200, clip.Width);
            for (int i = 0; i <= uniformCount; i++)
                xSet.Add(startX + (endX - startX) * i / uniformCount);

            // 2) Густе семплювання між кожною парою сусідніх вузлів
            var sorted = res.InputNodes.OrderBy(n => n.X).ToList();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                double x0 = sorted[i].X, x1 = sorted[i + 1].X;
                for (int k = 0; k <= 60; k++)
                    xSet.Add(x0 + (x1 - x0) * k / 60.0);
            }

            // 3) Невеликий запас за межами крайніх вузлів
            if (sorted.Count >= 2)
            {
                double nodeSpan = sorted.Last().X - sorted.First().X;
                double pad = nodeSpan * 0.15;
                for (int k = 0; k <= 20; k++)
                {
                    xSet.Add(sorted.First().X - pad + pad * k / 20.0);
                    xSet.Add(sorted.Last().X + pad * k / 20.0);
                }
            }

            double yClampMin = vYMin - (vYMax - vYMin) * 10;
            double yClampMax = vYMax + (vYMax - vYMin) * 10;

            Func<double, double> evalY = res.MethodName.Contains("Лагранж")
                ? x => LagrangeInterpolation.Evaluate(res.InputNodes, x, out _)
                : x => AitkenInterpolation.Evaluate(res.InputNodes, x, out _);

            var pts = new List<PointF>();
            foreach (double x in xSet)
            {
                double y = evalY(x);

                if (double.IsNaN(y) || double.IsInfinity(y))
                    y = yClampMax;
                y = Math.Max(yClampMin, Math.Min(yClampMax, y));

                float sx = tx(x);
                float sy = ty(y);

                sx = Math.Max(clip.Left - 200f, Math.Min(clip.Right + 200f, sx));
                sy = Math.Max(clip.Top - 200f, Math.Min(clip.Bottom + 200f, sy));

                pts.Add(new PointF(sx, sy));
            }

            if (pts.Count < 2) return;
            var arr = pts.ToArray();
            using var gl = new Pen(Color.FromArgb(30, c), 6f) { LineJoin = LineJoin.Round };
            using var pn = new Pen(c, 2.5f) { DashStyle = ds, LineJoin = LineJoin.Round };
            try { g.DrawLines(gl, arr); g.DrawLines(pn, arr); } catch { }
        }

        private void DrawNodes(Graphics g, List<InterpolationPoint> nodes,
            Func<double, float> tx, Func<double, float> ty, Rectangle clip)
        {
            using var fb = new SolidBrush(CNode);
            using var fp = new Pen(Color.White, 1.5f);
            using var ft = new Font("Consolas", 7.5f);
            using var tb = new SolidBrush(TextMain);
            using var bgBr = new SolidBrush(Color.FromArgb(200, BgMain));
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                float x = tx(n.X), y = ty(n.Y);
                if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) continue;
                if (x < clip.Left - 200 || x > clip.Right + 200 || y < clip.Top - 200 || y > clip.Bottom + 200) continue;

                g.FillEllipse(fb, x - 5, y - 5, 10, 10);
                g.DrawEllipse(fp, x - 5, y - 5, 10, 10);

                string label = $"({n.X:G7}; {n.Y:G7})";
                var sz = g.MeasureString(label, ft);
                float ly = (i % 2 == 0) ? y - 6 - sz.Height : y + 6;

                g.FillRectangle(bgBr, x + 7, ly, sz.Width, sz.Height);
                g.DrawString(label, ft, tb, x + 7, ly);
            }
        }

        private void DrawTarget(Graphics g, InterpolationResult r,
            Func<double, float> tx, Func<double, float> ty, Rectangle clip)
        {
            float x = tx(r.TargetX), y = ty(r.InterpolatedValue);
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) return;
            if (x < clip.Left - 200 || x > clip.Right + 200 || y < clip.Top - 200 || y > clip.Bottom + 200) return;

            using var fb = new SolidBrush(CTarget);
            using var fp = new Pen(Color.White, 2f);
            g.FillEllipse(fb, x - 6, y - 6, 12, 12);
            g.DrawEllipse(fp, x - 6, y - 6, 12, 12);
            string targetLabel = $"({r.TargetX:G7}; {r.InterpolatedValue:G7})";
            g.DrawString(targetLabel, new Font("Consolas", 7.5f, FontStyle.Bold),
                new SolidBrush(CTarget), x + 10, y - 10);
        }

        private static double NiceStep(double r, int t)
        {
            if (r <= 0) return 1; double raw = r / t, m = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double n = raw / m; return (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10) * m;
        }

        private static string Fmt(double v)
        {
            if (Math.Abs(v) < 1e-12) return "0";
            return Math.Abs(v) >= 1000000 || Math.Abs(v) < 0.001 ? v.ToString("G4") : Math.Round(v, 4).ToString("G7");
        }

        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.panelLeft = new System.Windows.Forms.Panel();
            this.panelLeftBottom = new System.Windows.Forms.Panel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCalc = new System.Windows.Forms.Button();
            this.cmbMethod = new System.Windows.Forms.ComboBox();
            this.labelMethod = new System.Windows.Forms.Label();
            this.txtX = new System.Windows.Forms.TextBox();
            this.labelX = new System.Windows.Forms.Label();
            this.btnLoad = new System.Windows.Forms.Button();
            this.btnDel = new System.Windows.Forms.Button();
            this.btnAdd = new System.Windows.Forms.Button();
            this.dgv = new System.Windows.Forms.DataGridView();
            this.colX = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colY = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.labelInputData = new System.Windows.Forms.Label();
            this.panelRight = new System.Windows.Forms.Panel();
            this.pnlChart = new InterpolationApp.DoubleBufferedPanel();
            this.pnlPoly = new System.Windows.Forms.Panel();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.errProv = new System.Windows.Forms.ErrorProvider(this.components);
            this.panelLeft.SuspendLayout();
            this.panelLeftBottom.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgv)).BeginInit();
            this.panelRight.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.errProv)).BeginInit();
            this.SuspendLayout();
            // 
            // panelLeft
            // 
            this.panelLeft.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(238)))), ((int)(((byte)(245)))));
            this.panelLeft.Controls.Add(this.dgv);
            this.panelLeft.Controls.Add(this.panelLeftBottom);
            this.panelLeft.Controls.Add(this.labelInputData);
            this.panelLeft.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelLeft.Location = new System.Drawing.Point(0, 0);
            this.panelLeft.Name = "panelLeft";
            this.panelLeft.Size = new System.Drawing.Size(240, 677);
            this.panelLeft.TabIndex = 0;
            // 
            // panelLeftBottom
            // 
            this.panelLeftBottom.Controls.Add(this.lblStatus);
            this.panelLeftBottom.Controls.Add(this.btnSave);
            this.panelLeftBottom.Controls.Add(this.btnCalc);
            this.panelLeftBottom.Controls.Add(this.cmbMethod);
            this.panelLeftBottom.Controls.Add(this.labelMethod);
            this.panelLeftBottom.Controls.Add(this.txtX);
            this.panelLeftBottom.Controls.Add(this.labelX);
            this.panelLeftBottom.Controls.Add(this.btnLoad);
            this.panelLeftBottom.Controls.Add(this.btnDel);
            this.panelLeftBottom.Controls.Add(this.btnAdd);
            this.panelLeftBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelLeftBottom.Location = new System.Drawing.Point(0, 327);
            this.panelLeftBottom.Name = "panelLeftBottom";
            this.panelLeftBottom.Size = new System.Drawing.Size(240, 350);
            this.panelLeftBottom.TabIndex = 2;
            // 
            // lblStatus
            // 
            this.lblStatus.BackColor = System.Drawing.Color.Transparent;
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Italic);
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(110)))), ((int)(((byte)(120)))));
            this.lblStatus.Location = new System.Drawing.Point(10, 265);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(220, 75);
            this.lblStatus.TabIndex = 11;
            // 
            // btnSave
            // 
            this.btnSave.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(160)))), ((int)(((byte)(110)))));
            this.btnSave.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSave.FlatAppearance.BorderSize = 0;
            this.btnSave.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(144)))), ((int)(((byte)(99)))));
            this.btnSave.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(174)))), ((int)(((byte)(131)))));
            this.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSave.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnSave.ForeColor = System.Drawing.Color.White;
            this.btnSave.Location = new System.Drawing.Point(10, 225);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(220, 30);
            this.btnSave.TabIndex = 10;
            this.btnSave.Text = "ЗБЕРЕГТИ ЗВІТ";
            this.btnSave.UseVisualStyleBackColor = false;
            this.btnSave.Click += new System.EventHandler(this.BtnSave_Click);
            // 
            // btnCalc
            // 
            this.btnCalc.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(100)))), ((int)(((byte)(220)))));
            this.btnCalc.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCalc.FlatAppearance.BorderSize = 0;
            this.btnCalc.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(90)))), ((int)(((byte)(198)))));
            this.btnCalc.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(72)))), ((int)(((byte)(123)))), ((int)(((byte)(225)))));
            this.btnCalc.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCalc.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.btnCalc.ForeColor = System.Drawing.Color.White;
            this.btnCalc.Location = new System.Drawing.Point(10, 175);
            this.btnCalc.Name = "btnCalc";
            this.btnCalc.Size = new System.Drawing.Size(220, 40);
            this.btnCalc.TabIndex = 9;
            this.btnCalc.Text = "ОБЧИСЛИТИ";
            this.btnCalc.UseVisualStyleBackColor = false;
            this.btnCalc.Click += new System.EventHandler(this.BtnCalc_Click);
            // 
            // cmbMethod
            // 
            this.cmbMethod.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.cmbMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbMethod.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cmbMethod.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.cmbMethod.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(34)))), ((int)(((byte)(40)))));
            this.cmbMethod.FormattingEnabled = true;
            this.cmbMethod.Items.AddRange(new object[] {
            "Лагранж",
            "Ейткен"});
            this.cmbMethod.Location = new System.Drawing.Point(80, 131);
            this.cmbMethod.Name = "cmbMethod";
            this.cmbMethod.Size = new System.Drawing.Size(150, 25);
            this.cmbMethod.TabIndex = 8;
            // 
            // labelMethod
            // 
            this.labelMethod.BackColor = System.Drawing.Color.Transparent;
            this.labelMethod.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.labelMethod.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(110)))), ((int)(((byte)(120)))));
            this.labelMethod.Location = new System.Drawing.Point(10, 134);
            this.labelMethod.Name = "labelMethod";
            this.labelMethod.Size = new System.Drawing.Size(65, 20);
            this.labelMethod.TabIndex = 7;
            this.labelMethod.Text = "Метод:";
            // 
            // txtX
            // 
            this.txtX.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.txtX.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtX.Font = new System.Drawing.Font("Consolas", 11F);
            this.txtX.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(34)))), ((int)(((byte)(40)))));
            this.txtX.Location = new System.Drawing.Point(80, 92);
            this.txtX.Name = "txtX";
            this.txtX.Size = new System.Drawing.Size(150, 25);
            this.txtX.TabIndex = 6;
            this.txtX.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.errProv.SetIconAlignment(this.txtX, System.Windows.Forms.ErrorIconAlignment.MiddleRight);
            this.errProv.SetIconPadding(this.txtX, -20);
            this.txtX.Validated += new System.EventHandler(this.TxtX_Validated);
            // 
            // labelX
            // 
            this.labelX.BackColor = System.Drawing.Color.Transparent;
            this.labelX.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.labelX.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(110)))), ((int)(((byte)(120)))));
            this.labelX.Location = new System.Drawing.Point(10, 95);
            this.labelX.Name = "labelX";
            this.labelX.Size = new System.Drawing.Size(65, 20);
            this.labelX.TabIndex = 5;
            this.labelX.Text = "Точка x:";
            // 
            // btnLoad
            // 
            this.btnLoad.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(80)))), ((int)(((byte)(180)))));
            this.btnLoad.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnLoad.FlatAppearance.BorderSize = 0;
            this.btnLoad.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(72)))), ((int)(((byte)(162)))));
            this.btnLoad.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(123)))), ((int)(((byte)(106)))), ((int)(((byte)(191)))));
            this.btnLoad.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLoad.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnLoad.ForeColor = System.Drawing.Color.White;
            this.btnLoad.Location = new System.Drawing.Point(10, 50);
            this.btnLoad.Name = "btnLoad";
            this.btnLoad.Size = new System.Drawing.Size(220, 30);
            this.btnLoad.TabIndex = 4;
            this.btnLoad.Text = "Файл";
            this.toolTip.SetToolTip(this.btnLoad, "Формат файлу (.txt або .csv):\nКожен рядок — це координати X та Y.\nРозділювачі: пробіл, крапка з комою або табуляція.\n(Кома використовується для десяткових дробів)\n\nПриклад:\n1,5; 2,7\n-3,0; 4,1");
            this.btnLoad.UseVisualStyleBackColor = false;
            this.btnLoad.Click += new System.EventHandler(this.BtnLoad_Click);
            // 
            // btnDel
            // 
            this.btnDel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnDel.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnDel.FlatAppearance.BorderSize = 0;
            this.btnDel.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(54)))), ((int)(((byte)(54)))));
            this.btnDel.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(208)))), ((int)(((byte)(89)))), ((int)(((byte)(89)))));
            this.btnDel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDel.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnDel.ForeColor = System.Drawing.Color.White;
            this.btnDel.Location = new System.Drawing.Point(125, 10);
            this.btnDel.Name = "btnDel";
            this.btnDel.Size = new System.Drawing.Size(105, 30);
            this.btnDel.TabIndex = 3;
            this.btnDel.Text = "−";
            this.btnDel.UseVisualStyleBackColor = false;
            this.btnDel.Click += new System.EventHandler(this.BtnDel_Click);
            // 
            // btnAdd
            // 
            this.btnAdd.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(160)))), ((int)(((byte)(90)))));
            this.btnAdd.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnAdd.FlatAppearance.BorderSize = 0;
            this.btnAdd.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(144)))), ((int)(((byte)(81)))));
            this.btnAdd.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(174)))), ((int)(((byte)(114)))));
            this.btnAdd.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAdd.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnAdd.ForeColor = System.Drawing.Color.White;
            this.btnAdd.Location = new System.Drawing.Point(10, 10);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(105, 30);
            this.btnAdd.TabIndex = 2;
            this.btnAdd.Text = "+";
            this.btnAdd.UseVisualStyleBackColor = false;
            this.btnAdd.Click += new System.EventHandler(this.BtnAdd_Click);
            // 
            // dgv
            // 
            this.dgv.AllowUserToAddRows = false;
            this.dgv.AllowUserToDeleteRows = false;
            this.dgv.AllowUserToResizeColumns = false;
            this.dgv.AllowUserToResizeRows = false;
            this.dgv.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.dgv.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dgv.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(235)))), ((int)(((byte)(235)))));
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Segoe UI Semibold", 10F);
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(34)))), ((int)(((byte)(40)))));
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(235)))), ((int)(((byte)(235)))));
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(34)))), ((int)(((byte)(40)))));
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dgv.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dgv.ColumnHeadersHeight = 32;
            this.dgv.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colX,
            this.colY});
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Consolas", 11F);
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(34)))), ((int)(((byte)(40)))));
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(210)))), ((int)(((byte)(230)))));
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(34)))), ((int)(((byte)(40)))));
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dgv.DefaultCellStyle = dataGridViewCellStyle2;
            this.dgv.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgv.EnableHeadersVisualStyles = false;
            this.dgv.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(210)))), ((int)(((byte)(215)))), ((int)(((byte)(225)))));
            this.dgv.Location = new System.Drawing.Point(0, 45);
            this.dgv.Name = "dgv";
            this.dgv.RowHeadersVisible = false;
            this.dgv.RowTemplate.Height = 25;
            this.dgv.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.dgv.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dgv.Size = new System.Drawing.Size(240, 282);
            this.dgv.TabIndex = 1;
            this.dgv.CellValidated += new System.Windows.Forms.DataGridViewCellEventHandler(this.Dgv_CellValidated);
            this.colX.HeaderText = "X";
            this.colX.Name = "colX";
            this.colX.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.colX.Width = 120;
            this.colY.HeaderText = "Y";
            this.colY.Name = "colY";
            this.colY.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.colY.Width = 120;
            this.labelInputData.BackColor = System.Drawing.Color.Transparent;
            this.labelInputData.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelInputData.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.labelInputData.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(100)))), ((int)(((byte)(220)))));
            this.labelInputData.Location = new System.Drawing.Point(0, 0);
            this.labelInputData.Name = "labelInputData";
            this.labelInputData.Padding = new System.Windows.Forms.Padding(10, 15, 0, 0);
            this.labelInputData.Size = new System.Drawing.Size(240, 45);
            this.labelInputData.TabIndex = 0;
            this.labelInputData.Text = "ВХІДНІ ДАНІ";
            this.panelRight.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this.panelRight.Controls.Add(this.pnlChart);
            this.panelRight.Controls.Add(this.pnlPoly);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRight.Location = new System.Drawing.Point(240, 0);
            this.panelRight.Name = "panelRight";
            this.panelRight.Padding = new System.Windows.Forms.Padding(10);
            this.panelRight.Size = new System.Drawing.Size(577, 677);
            this.panelRight.TabIndex = 1;
            this.pnlChart.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this.pnlChart.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlChart.Location = new System.Drawing.Point(10, 110);
            this.pnlChart.Name = "pnlChart";
            this.pnlChart.Size = new System.Drawing.Size(557, 557);
            this.pnlChart.TabIndex = 1;
            this.pnlChart.Paint += new System.Windows.Forms.PaintEventHandler(this.PnlChart_Paint);
            this.pnlChart.MouseDown += new System.Windows.Forms.MouseEventHandler(this.PnlChart_MouseDown);
            this.pnlChart.MouseMove += new System.Windows.Forms.MouseEventHandler(this.PnlChart_MouseMove);
            this.pnlChart.MouseUp += new System.Windows.Forms.MouseEventHandler(this.PnlChart_MouseUp);
            this.pnlChart.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.PnlChart_MouseWheel);
            this.pnlChart.Resize += new System.EventHandler(this.PnlChart_Resize);
            this.pnlPoly.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(252)))), ((int)(((byte)(255)))));
            this.pnlPoly.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlPoly.Location = new System.Drawing.Point(10, 10);
            this.pnlPoly.Name = "pnlPoly";
            this.pnlPoly.Size = new System.Drawing.Size(557, 100);
            this.pnlPoly.TabIndex = 0;
            this.pnlPoly.Paint += new System.Windows.Forms.PaintEventHandler(this.PnlPoly_Paint);
            this.toolTip.AutoPopDelay = 10000;
            this.toolTip.InitialDelay = 500;
            this.toolTip.ReshowDelay = 500;
            this.errProv.BlinkStyle = System.Windows.Forms.ErrorBlinkStyle.NeverBlink;
            this.errProv.ContainerControl = this;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this.ClientSize = new System.Drawing.Size(817, 677);
            this.Controls.Add(this.panelRight);
            this.Controls.Add(this.panelLeft);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Інтерполяція поліномів";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.panelLeft.ResumeLayout(false);
            this.panelLeftBottom.ResumeLayout(false);
            this.panelLeftBottom.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgv)).EndInit();
            this.panelRight.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.errProv)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelLeft;
        private System.Windows.Forms.Panel panelLeftBottom;
        private System.Windows.Forms.Label labelInputData;
        private System.Windows.Forms.DataGridView dgv;
        private System.Windows.Forms.DataGridViewTextBoxColumn colX;
        private System.Windows.Forms.DataGridViewTextBoxColumn colY;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnDel;
        private System.Windows.Forms.Button btnLoad;
        private System.Windows.Forms.Label labelX;
        private System.Windows.Forms.TextBox txtX;
        private System.Windows.Forms.Label labelMethod;
        private System.Windows.Forms.ComboBox cmbMethod;
        private System.Windows.Forms.Button btnCalc;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Panel panelRight;
        private System.Windows.Forms.Panel pnlPoly;
        private InterpolationApp.DoubleBufferedPanel pnlChart;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.ErrorProvider errProv;
        }

    internal class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
        }
    }
}
