using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public enum AspectPictureBoxMode
    {
        Stretch,
        Fit,
        CenterFit
    }

    public class AspectPictureBox : PictureBox
    {
        private int _ratioWidth = 16;
        private int _ratioHeight = 9;
        private AspectPictureBoxMode _aspectMode = AspectPictureBoxMode.CenterFit;

        public AspectPictureBox()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint
                | ControlStyles.SupportsTransparentBackColor,
                true);

            SizeMode = PictureBoxSizeMode.Zoom;
        }

        [DefaultValue(16)]
        [Category("Aspect")]
        [Description("The width side of the display ratio.")]
        public int RatioWidth
        {
            get => _ratioWidth;
            set
            {
                int newValue = Math.Max(1, value);
                if (_ratioWidth == newValue)
                {
                    return;
                }

                _ratioWidth = newValue;
                Invalidate();
            }
        }

        [DefaultValue(9)]
        [Category("Aspect")]
        [Description("The height side of the display ratio.")]
        public int RatioHeight
        {
            get => _ratioHeight;
            set
            {
                int newValue = Math.Max(1, value);
                if (_ratioHeight == newValue)
                {
                    return;
                }

                _ratioHeight = newValue;
                Invalidate();
            }
        }

        [DefaultValue(AspectPictureBoxMode.CenterFit)]
        [Category("Aspect")]
        [Description("Controls how the ratio rectangle is placed inside the control bounds.")]
        public AspectPictureBoxMode AspectMode
        {
            get => _aspectMode;
            set
            {
                if (_aspectMode == value)
                {
                    return;
                }

                _aspectMode = value;
                Invalidate();
            }
        }

        [Browsable(false)]
        public float AspectRatio => RatioWidth / (float)RatioHeight;

        [Browsable(false)]
        public Rectangle AspectBounds => GetAspectBounds(ClientRectangle);

        public Size GetBestFitSize(Size availableSize)
        {
            Rectangle bounds = GetAspectBounds(new Rectangle(Point.Empty, availableSize));
            return bounds.Size;
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            if (AspectMode == AspectPictureBoxMode.Stretch)
            {
                base.OnPaint(pe);
                return;
            }

            Rectangle aspectBounds = AspectBounds;
            if (aspectBounds.Width <= 0 || aspectBounds.Height <= 0)
            {
                return;
            }

            Graphics graphics = pe.Graphics;
            graphics.Clear(BackColor);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            DrawBackgroundImage(graphics, aspectBounds);

            if (Image == null)
            {
                return;
            }

            Rectangle imageBounds = GetImageBounds(Image.Size, aspectBounds);
            if (imageBounds.Width <= 0 || imageBounds.Height <= 0)
            {
                return;
            }

            if (Enabled)
            {
                graphics.DrawImage(Image, imageBounds);
            }
            else
            {
                ControlPaint.DrawImageDisabled(graphics, Image, imageBounds.X, imageBounds.Y, BackColor);
            }
        }

        private Rectangle GetAspectBounds(Rectangle availableBounds)
        {
            if (availableBounds.Width <= 0 || availableBounds.Height <= 0)
            {
                return Rectangle.Empty;
            }

            int width = availableBounds.Width;
            int height = (int)Math.Round(width / AspectRatio);

            if (height > availableBounds.Height)
            {
                height = availableBounds.Height;
                width = (int)Math.Round(height * AspectRatio);
            }

            int x = availableBounds.X;
            int y = availableBounds.Y;
            if (AspectMode == AspectPictureBoxMode.CenterFit)
            {
                x += Math.Max(0, (availableBounds.Width - width) / 2);
                y += Math.Max(0, (availableBounds.Height - height) / 2);
            }

            return new Rectangle(x, y, Math.Max(1, width), Math.Max(1, height));
        }

        private Rectangle GetImageBounds(Size imageSize, Rectangle displayBounds)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return Rectangle.Empty;
            }

            return SizeMode switch
            {
                PictureBoxSizeMode.StretchImage => displayBounds,
                PictureBoxSizeMode.Zoom => FitImageInsideBounds(imageSize, displayBounds),
                PictureBoxSizeMode.CenterImage => CenterImageInBounds(imageSize, displayBounds),
                _ => new Rectangle(displayBounds.Location, imageSize)
            };
        }

        private static Rectangle FitImageInsideBounds(Size imageSize, Rectangle bounds)
        {
            float scale = Math.Min(
                bounds.Width / (float)imageSize.Width,
                bounds.Height / (float)imageSize.Height);

            int width = Math.Max(1, (int)Math.Round(imageSize.Width * scale));
            int height = Math.Max(1, (int)Math.Round(imageSize.Height * scale));
            int x = bounds.X + Math.Max(0, (bounds.Width - width) / 2);
            int y = bounds.Y + Math.Max(0, (bounds.Height - height) / 2);
            return new Rectangle(x, y, width, height);
        }

        private static Rectangle CenterImageInBounds(Size imageSize, Rectangle bounds)
        {
            int x = bounds.X + (bounds.Width - imageSize.Width) / 2;
            int y = bounds.Y + (bounds.Height - imageSize.Height) / 2;
            return new Rectangle(x, y, imageSize.Width, imageSize.Height);
        }

        private void DrawBackgroundImage(Graphics graphics, Rectangle bounds)
        {
            if (BackgroundImage == null)
            {
                return;
            }

            switch (BackgroundImageLayout)
            {
                case ImageLayout.None:
                    graphics.DrawImageUnscaled(BackgroundImage, bounds.Location);
                    break;
                case ImageLayout.Center:
                    {
                        Rectangle imageBounds = CenterImageInBounds(BackgroundImage.Size, bounds);
                        graphics.DrawImageUnscaled(BackgroundImage, imageBounds.Location);
                        break;
                    }
                case ImageLayout.Stretch:
                    graphics.DrawImage(BackgroundImage, bounds);
                    break;
                case ImageLayout.Zoom:
                    graphics.DrawImage(BackgroundImage, FitImageInsideBounds(BackgroundImage.Size, bounds));
                    break;
                default:
                    using (TextureBrush brush = new TextureBrush(BackgroundImage, WrapMode.Tile))
                    {
                        brush.TranslateTransform(bounds.X, bounds.Y);
                        graphics.FillRectangle(brush, bounds);
                    }
                    break;
            }
        }
    }
}
