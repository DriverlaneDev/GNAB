using System.Drawing.Imaging;
using Gnab.Settings;
using Encoder = System.Drawing.Imaging.Encoder;

namespace Gnab.Imaging;

public static class ImageSaver
{
    private static readonly ImageCodecInfo JpegEncoder = ImageCodecInfo.GetImageEncoders()
        .First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);

    public static string Extension(ImageFormatOption option) => option switch
    {
        ImageFormatOption.Png => "png",
        ImageFormatOption.Jpeg => "jpg",
        ImageFormatOption.Bmp => "bmp",
        ImageFormatOption.Gif => "gif",
        ImageFormatOption.Tiff => "tiff",
        _ => "png"
    };

    public static string SaveDialogFilter(ImageFormatOption option)
    {
        var ext = Extension(option);
        return $"{ext.ToUpperInvariant()} Image (*.{ext})|*.{ext}|All Files (*.*)|*.*";
    }

    public static void Save(Bitmap bitmap, string path, ImageFormatOption option, int jpegQuality)
    {
        if (option == ImageFormatOption.Jpeg)
        {
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(Encoder.Quality, (long)Math.Clamp(jpegQuality, 1, 100));
            bitmap.Save(path, JpegEncoder, parameters);
            return;
        }

        var format = option switch
        {
            ImageFormatOption.Bmp => ImageFormat.Bmp,
            ImageFormatOption.Gif => ImageFormat.Gif,
            ImageFormatOption.Tiff => ImageFormat.Tiff,
            _ => ImageFormat.Png
        };
        bitmap.Save(path, format);
    }
}
