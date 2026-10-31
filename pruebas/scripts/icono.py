"""Genera el ícono de PRINT ATH: impresora blanca con foto sobre fondo azul redondeado."""
import sys
from PIL import Image, ImageDraw

S = 512
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
d.rounded_rectangle((16, 16, S - 16, S - 16), radius=110, fill=(30, 111, 217))

blanco = (255, 255, 255)
azul = (30, 111, 217)
# Hoja con foto que sale por arriba
d.rounded_rectangle((150, 70, 362, 250), radius=14, fill=blanco)
d.polygon([(170, 230), (230, 150), (275, 205), (305, 175), (345, 230)], fill=(143, 180, 234))
d.ellipse((300, 98, 336, 134), fill=(143, 180, 234))
# Cuerpo de la impresora
d.rounded_rectangle((86, 220, 426, 380), radius=34, fill=blanco)
d.rounded_rectangle((136, 330, 376, 360), radius=8, fill=azul)
d.ellipse((370, 254, 394, 278), fill=azul)
# Bandeja de salida
d.rounded_rectangle((150, 345, 362, 440), radius=14, fill=blanco)
d.rectangle((150, 345, 362, 362), fill=azul)

img.save(sys.argv[1], sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
img.save(sys.argv[1].replace(".ico", "-vista.png"))
