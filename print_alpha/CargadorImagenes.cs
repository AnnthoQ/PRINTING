using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PrintATH;

/// <summary>Dimensiones crudas del archivo y su orientación EXIF (1 a 8).</summary>
public sealed record InfoImagen(int AnchoCrudo, int AltoCrudo, int Orientacion)
{
    bool Traspuesta => Orientacion is 5 or 6 or 7 or 8;
    public int Ancho => Traspuesta ? AltoCrudo : AnchoCrudo;
    public int Alto => Traspuesta ? AnchoCrudo : AltoCrudo;
}

static class CargadorImagenes
{
    // Lo que WIC decodifica de serie; HEIC, WebP y AVIF dependen de las extensiones instaladas en Windows.
    static readonly string[] Extensiones =
    {
        ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".bmp", ".dib", ".gif", ".tif", ".tiff",
        ".ico", ".wdp", ".jxr", ".heic", ".heif", ".webp", ".avif",
    };

    public static string FiltroDialogo =>
        "Imágenes|" + string.Join(";", Extensiones.Select(e => "*" + e)) + "|Todos los archivos|*.*";

    public static bool EsImagen(string ruta) =>
        Extensiones.Contains(Path.GetExtension(ruta), StringComparer.OrdinalIgnoreCase);

    static FileStream Abrir(string ruta) =>
        new(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    public static InfoImagen LeerInfo(string ruta)
    {
        using var flujo = Abrir(ruta);
        var decodificador = BitmapDecoder.Create(flujo, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        var cuadro = decodificador.Frames[0];
        int orientacion = 1;
        try
        {
            if (cuadro.Metadata is BitmapMetadata meta &&
                meta.GetQuery("System.Photo.Orientation") is ushort valor && valor is >= 1 and <= 8)
                orientacion = valor;
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            // Formatos sin metadatos (BMP, ICO…): se queda la orientación normal.
        }
        return new InfoImagen(cuadro.PixelWidth, cuadro.PixelHeight, orientacion);
    }

    /// <summary>
    /// Decodifica la imagen con su lado mayor limitado a <paramref name="ladoMaximo"/> píxeles (0 = tamaño original)
    /// y la endereza según EXIF. El resultado está congelado y se puede usar desde cualquier hilo.
    /// </summary>
    public static BitmapSource Cargar(string ruta, InfoImagen info, int ladoMaximo)
    {
        var imagen = new BitmapImage();
        using (var flujo = Abrir(ruta))
        {
            imagen.BeginInit();
            imagen.CacheOption = BitmapCacheOption.OnLoad;
            imagen.StreamSource = flujo;
            if (ladoMaximo > 0 && Math.Max(info.AnchoCrudo, info.AltoCrudo) > ladoMaximo)
            {
                if (info.AnchoCrudo >= info.AltoCrudo) imagen.DecodePixelWidth = ladoMaximo;
                else imagen.DecodePixelHeight = ladoMaximo;
            }
            imagen.EndInit();
        }
        imagen.Freeze();
        return Enderezar(imagen, info.Orientacion);
    }

    static BitmapSource Enderezar(BitmapSource imagen, int orientacion)
    {
        Transform? giro = orientacion switch
        {
            2 => new ScaleTransform(-1, 1),
            3 => new RotateTransform(180),
            4 => new ScaleTransform(1, -1),
            5 => new TransformGroup { Children = { new RotateTransform(90), new ScaleTransform(-1, 1) } },
            6 => new RotateTransform(90),
            7 => new TransformGroup { Children = { new RotateTransform(270), new ScaleTransform(-1, 1) } },
            8 => new RotateTransform(270),
            _ => null,
        };
        return giro is null ? imagen : Congelar(new TransformedBitmap(imagen, giro));
    }

    public static BitmapSource Congelar(BitmapSource imagen)
    {
        imagen.Freeze();
        return imagen;
    }
}
