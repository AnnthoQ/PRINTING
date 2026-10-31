using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forma = System.Windows.Shapes;

namespace PrintATH;

/// <summary>Cómo se dibuja una foto en su marco: zona de la imagen, si se gira 90° y a qué resolución decodificarla.</summary>
sealed record Plan(Rect MarcoImagen, bool Girar, int Lado);

static class Compositor
{
    /// <summary>Reparte las fotos (con sus copias) en hojas. Con <paramref name="repetir"/> cada foto llena hojas enteras.</summary>
    public static List<List<Colocacion>> Paginar(IReadOnlyList<Foto> fotos, IReadOnlyList<Rect> celdas, bool repetir)
    {
        var paginas = new List<List<Colocacion>>();
        if (celdas.Count == 0) return paginas;

        var actual = new List<Colocacion>();
        void Cerrar()
        {
            if (actual.Count == 0) return;
            paginas.Add(actual);
            actual = new List<Colocacion>();
        }

        foreach (var foto in fotos)
        {
            if (repetir) Cerrar();
            int veces = foto.Copias * (repetir ? celdas.Count : 1);
            for (int i = 0; i < veces; i++)
            {
                actual.Add(new Colocacion(celdas[actual.Count], foto));
                if (actual.Count == celdas.Count) Cerrar();
            }
        }
        Cerrar();
        return paginas;
    }

    public static double AltoPie(Rect marco) => Math.Clamp(marco.Height * 0.14, 9, 16);

    public static Plan Planificar(Colocacion c, OpcionesComposicion op)
    {
        var marco = c.Marco;
        if (op.Pie)
            marco = new Rect(marco.X, marco.Y, marco.Width, Math.Max(1, marco.Height - AltoPie(marco)));

        var f = c.Foto;
        bool girar = op.GirarAuto && f.Ancho != f.Alto && Math.Abs(marco.Width - marco.Height) > 0.5 &&
                     (f.Ancho > f.Alto) != (marco.Width > marco.Height);
        double ancho = girar ? f.Alto : f.Ancho, alto = girar ? f.Ancho : f.Alto;

        int lado;
        if (ancho <= 0 || alto <= 0)
        {
            lado = (int)Math.Ceiling(Math.Max(marco.Width, marco.Height) / 96 * op.Dpi);
        }
        else
        {
            // La foto siempre entra completa en su marco (nunca se recorta).
            double escala = Math.Min(marco.Width / ancho, marco.Height / alto);
            lado = (int)Math.Ceiling(Math.Max(ancho, alto) * escala / 96 * op.Dpi);
            lado = (int)Math.Min(lado, Math.Max(ancho, alto));
        }
        return new Plan(marco, girar, Math.Max(lado, 16));
    }

    public static Canvas ConstruirPagina(Hoja hoja, IReadOnlyList<Colocacion> colocaciones,
                                            OpcionesComposicion op, ProveedorImagenes proveedor)
    {
        var pagina = new Canvas { Width = hoja.Ancho, Height = hoja.Alto, Background = Brushes.White, ClipToBounds = true };

        foreach (var c in colocaciones)
        {
            var plan = Planificar(c, op);
            Ubicar(pagina, Imagen(c.Foto, plan, op, proveedor), plan.MarcoImagen);

            if (op.Pie)
            {
                double alto = AltoPie(c.Marco);
                var texto = new TextBlock
                {
                    Text = c.Foto.Nombre,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = Math.Clamp(alto * 0.62, 6, 10),
                    Foreground = Brushes.Black,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Width = c.Marco.Width,
                };
                Ubicar(pagina, texto, new Rect(c.Marco.X, plan.MarcoImagen.Bottom + 1, c.Marco.Width, alto));
            }
        }
        return pagina;
    }

    static UIElement Imagen(Foto foto, Plan plan, OpcionesComposicion op, ProveedorImagenes proveedor)
    {
        try
        {
            var pincel = new ImageBrush(proveedor.Obtener(foto, plan.Lado, plan.Girar, op.Grises))
            {
                Stretch = Stretch.Uniform,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
            };
            pincel.Freeze();
            var rect = new Forma.Rectangle { Width = plan.MarcoImagen.Width, Height = plan.MarcoImagen.Height, Fill = pincel };
            RenderOptions.SetBitmapScalingMode(rect, BitmapScalingMode.HighQuality);
            return rect;
        }
        catch (Exception)
        {
            return new Border
            {
                Width = plan.MarcoImagen.Width,
                Height = plan.MarcoImagen.Height,
                Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
                Child = new TextBlock
                {
                    Text = "No se pudo cargar",
                    FontSize = 9,
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
        }
    }

    static void Ubicar(Canvas pagina, UIElement elemento, Rect donde)
    {
        Canvas.SetLeft(elemento, donde.X);
        Canvas.SetTop(elemento, donde.Y);
        pagina.Children.Add(elemento);
    }
}

/// <summary>
/// Entrega las fotos decodificadas al tamaño justo. En la vista previa guarda en caché por tramos de
/// resolución; al imprimir decodifica cada página y la suelta para no acumular memoria.
/// </summary>
sealed class ProveedorImagenes
{
    static readonly int[] Tramos = { 192, 256, 384, 512, 768, 1024, 1536, 2048, 3072, 4096, 6144, 8192 };

    readonly bool cachear;
    readonly ConcurrentDictionary<(string Ruta, int Lado), Lazy<BitmapSource>> cache = new();

    public ProveedorImagenes(bool cachear) => this.cachear = cachear;

    public void Olvidar(string ruta)
    {
        foreach (var clave in cache.Keys.Where(k => k.Ruta == ruta).ToList())
            cache.TryRemove(clave, out _);
    }

    BitmapSource Base(Foto foto, int lado)
    {
        var info = foto.Info ?? throw new InvalidOperationException("La imagen no está lista.");
        int original = Math.Max(info.Ancho, info.Alto);
        if (!cachear)
            return CargadorImagenes.Cargar(foto.Ruta, info, lado >= original ? 0 : lado);

        int tramo = Tramos.FirstOrDefault(t => t >= lado);
        if (tramo == 0 || tramo >= original) tramo = 0;
        return cache.GetOrAdd((foto.Ruta, tramo),
            clave => new Lazy<BitmapSource>(() => CargadorImagenes.Cargar(clave.Ruta, info, clave.Lado))).Value;
    }

    public BitmapSource Obtener(Foto foto, int lado, bool girar, bool grises)
    {
        var imagen = Base(foto, lado);
        int giro = (foto.RotacionManual + (girar ? 90 : 0)) % 360;
        if (giro != 0)
            imagen = CargadorImagenes.Congelar(new TransformedBitmap(imagen, new RotateTransform(giro)));
        if (grises)
            imagen = CargadorImagenes.Congelar(new FormatConvertedBitmap(imagen, PixelFormats.Gray8, null, 0));
        return imagen;
    }

    /// <summary>Decodifica en segundo plano lo que necesita una página para que la vista previa no congele la ventana.</summary>
    public Task PrecargarAsync(IReadOnlyList<Colocacion> colocaciones, OpcionesComposicion op)
    {
        var pendientes = colocaciones.Select(c => (c.Foto, Compositor.Planificar(c, op).Lado))
                                     .DistinctBy(p => (p.Foto.Ruta, p.Lado))
                                     .ToList();
        return Task.Run(() => Parallel.ForEach(pendientes, new ParallelOptions { MaxDegreeOfParallelism = 4 }, p =>
        {
            try { Base(p.Foto, p.Lado); }
            catch (Exception) { /* Se mostrará el marcador de error al construir la página. */ }
        }));
    }
}

/// <summary>Genera las páginas de una en una mientras el escritor XPS las envía a la cola de impresión.</summary>
sealed class PaginadorFotos : DocumentPaginator
{
    readonly IReadOnlyList<List<Colocacion>> paginas;
    readonly Hoja hoja;
    readonly OpcionesComposicion op;
    readonly ProveedorImagenes proveedor;

    public PaginadorFotos(IReadOnlyList<List<Colocacion>> paginas, Hoja hoja, OpcionesComposicion op, ProveedorImagenes proveedor)
    {
        this.paginas = paginas;
        this.hoja = hoja;
        this.op = op;
        this.proveedor = proveedor;
    }

    public override DocumentPage GetPage(int numero)
    {
        var pagina = Compositor.ConstruirPagina(hoja, paginas[numero], op, proveedor);
        var tamano = new Size(hoja.Ancho, hoja.Alto);
        pagina.Measure(tamano);
        pagina.Arrange(new Rect(tamano));
        pagina.UpdateLayout();
        return new DocumentPage(pagina, tamano, new Rect(tamano), new Rect(tamano));
    }

    public override bool IsPageCountValid => true;
    public override int PageCount => paginas.Count;
    public override Size PageSize { get => new(hoja.Ancho, hoja.Alto); set { } }
    public override IDocumentPaginatorSource? Source => null;
}
