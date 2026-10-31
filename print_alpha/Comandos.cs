using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps.Packaging;

namespace PrintATH;

/// <summary>Opciones de línea de comandos que no abren la ventana.</summary>
static class Comandos
{
    public static string? ValorDe(string[] args, string opcion)
    {
        int i = Array.IndexOf(args, opcion);
        if (i >= 0 && i + 1 < args.Length) return args[i + 1];
        string prefijo = opcion + "=";
        return args.FirstOrDefault(a => a.StartsWith(prefijo, StringComparison.Ordinal))?[prefijo.Length..];
    }

    /// <summary>Devuelve el código de salida si el argumento era un comando, o null para abrir la ventana.</summary>
    public static int? Ejecutar(string[] args)
    {
        if (args.Length == 0) return null;
        bool silencioso = args.Contains("--silencioso");
        try
        {
            switch (args[0])
            {
                case "--instalar":
                    string exe = MenuContextual.Instalar();
                    Avisar(silencioso, $"Listo. Selecciona imágenes en el Explorador, haz clic derecho y elige «Print Menu Alpha» o «Print Fast Alpha».\n\nPrograma: {exe}");
                    return 0;
                case "--registrar":
                    MenuContextual.Registrar(Environment.ProcessPath!);
                    Avisar(silencioso, "«Print Menu Alpha» y «Print Fast Alpha» se agregaron al menú contextual de las imágenes.");
                    return 0;
                case "--desregistrar":
                    MenuContextual.Desregistrar();
                    Avisar(silencioso, "«Print Menu Alpha» y «Print Fast Alpha» se quitaron del menú contextual.");
                    return 0;
                case "--prueba-xps":
                    return PruebaXps(args);
                default:
                    return null;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            if (!silencioso)
                MessageBox.Show(ex.Message, "Print Menu Alpha", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }

    static void Avisar(bool silencioso, string texto)
    {
        if (!silencioso)
            MessageBox.Show(texto, "Print Menu Alpha", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// Diagnóstico sin impresora: compone las fotos en A4, escribe un XPS por la misma vía que la impresión real y
    /// exporta cada página como PNG. Uso: --prueba-xps salida.xps --porhoja=4 [--exacto=30x40] [--escala=90] foto1.jpg …
    /// </summary>
    static int PruebaXps(string[] args)
    {
        string salida = Path.GetFullPath(args[1]);
        var diseno = Diseno.PorCantidad(int.Parse(ValorDe(args, "--porhoja") ?? "1"));
        if (ValorDe(args, "--exacto")?.Split('x') is [var ancho, var alto])
            diseno = Diseno.Exacto(double.Parse(ancho, System.Globalization.CultureInfo.InvariantCulture),
                                   double.Parse(alto, System.Globalization.CultureInfo.InvariantCulture));
        var fotos = args.Skip(2).Where(a => !a.StartsWith("--", StringComparison.Ordinal)).Select(r =>
        {
            var f = new Foto(Path.GetFullPath(r));
            f.EstablecerInfo(CargadorImagenes.LeerInfo(f.Ruta));
            return f;
        }).ToList();

        double escala = Math.Clamp(double.Parse(ValorDe(args, "--escala") ?? "90"), 50, 100) / 100;
        var hoja = Hoja.A4.Escalada(escala);
        bool nombre = args.Contains("--nombre");
        var op = new OpcionesComposicion(GirarAuto: !nombre, Pie: nombre, Grises: args.Contains("--grises"), Dpi: 150);
        var paginas = Compositor.Paginar(fotos, Geometria.Celdas(diseno, hoja), args.Contains("--repetir"));
        var paginador = new PaginadorFotos(paginas, hoja, op, new ProveedorImagenes(cachear: false));

        File.Delete(salida);
        using (var documento = new XpsDocument(salida, FileAccess.ReadWrite, CompressionOption.Maximum))
            XpsDocument.CreateXpsDocumentWriter(documento).Write(paginador);

        // Se vuelve a leer el XPS escrito para comprobar el resultado tal como lo recibiría la impresora.
        using (var documento = new XpsDocument(salida, FileAccess.Read))
        {
            var leido = documento.GetFixedDocumentSequence().DocumentPaginator;
            for (int i = 0; i < leido.PageCount; i++)
            {
                var pagina = leido.GetPage(i);
                var mapa = new RenderTargetBitmap((int)pagina.Size.Width, (int)pagina.Size.Height, 96, 96, PixelFormats.Pbgra32);
                var fondo = new DrawingVisual();
                using (var dc = fondo.RenderOpen())
                    dc.DrawRectangle(Brushes.White, null, new Rect(pagina.Size));
                mapa.Render(fondo);
                mapa.Render(pagina.Visual);
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(mapa));
                using var archivo = File.Create(Path.ChangeExtension(salida, null) + $"-{i + 1}.png");
                png.Save(archivo);
            }
            Console.WriteLine($"{leido.PageCount} página(s)");
        }
        return 0;
    }
}
