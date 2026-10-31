# Ingeniería inversa: impresión de imágenes de Windows

| Carpeta | Contenido |
|---|---|
| [`analisis_photowiz/`](analisis_photowiz/INFORME.md) | Análisis con REA + Ghidra del asistente «Imprimir imágenes» de Windows (`photowiz.dll`): informe, 919 funciones descompiladas, plantillas y cadenas |
| [`print_alpha/`](print_alpha/README.md) | **Print Alpha v1**: programa propio que lo reemplaza. Código fuente en C#/WPF y `dist\PrintATH.exe` listo para instalar |
| `pruebas/` | Fotos de prueba, los scripts que las generan y las páginas/capturas de cada iteración (`salidas/salida` → `salida5`) |

Print Alpha agrega dos opciones al clic derecho de las imágenes:

- **Print Menu Alpha**: menú completo con vista previa, impresora, papel, fotos por hoja (1-1 a 30-1) y tamaño en la hoja.
- **Print Fast Alpha**: ventanita con 1-1 a 4-1 y los botones 920 / CANON / RICOH; imprime en A4 y se cierra.

Para instalarlo en una PC: copiar `print_alpha\dist\PrintATH.exe`, abrirlo y pulsar «Agregar al clic derecho»
(o `PrintATH.exe --instalar`).
