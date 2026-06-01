import serial
import time
import base64
from PIL import Image

# ========================================================
# Configuración
# ========================================================
# Cambia 'COM3' por el puerto serial de tu Arduino Mega
SERIAL_PORT = 'COM5' 
BAUD_RATE = 115200

# Tamaño de la imagen configurado en el Arduino (QQVGA)
WIDTH = 160
HEIGHT = 120

def save_image():
    try:
        # Abrir conexión
        ser = serial.Serial(SERIAL_PORT, BAUD_RATE, timeout=2)
        print(f"✅ Conectado a {SERIAL_PORT} a {BAUD_RATE} baudios.")
    except Exception as e:
        print(f"❌ Error abriendo puerto serial: {e}")
        return

    # Esperar a que el Arduino reinicie al abrir el serial
    time.sleep(2)

    # 1. Enviar el comando de configuración
    print("⚙️ Enviando comando 'config'...")
    ser.write(b"config\n")
    time.sleep(2) # Dar tiempo a que termine de configurar
    
    # Limpiar buffer de lectura
    ser.reset_input_buffer()

    # 2. Solicitar la captura
    print("📸 Enviando comando 'capture'...")
    ser.write(b"capture\n")

    capturing = False
    base64_data = ""
    empty_count = 0

    print("⏳ Esperando datos de la imagen...")

    while True:
        try:
            line = ser.readline().decode('utf-8', errors='ignore').strip()
        except:
            continue

        if not line:
            empty_count += 1
            if empty_count > 10:
                print("❌ Timeout: no se recibió CAM:END (sin datos por ~20 s). Revisa la conexión.")
                ser.close()
                return
            continue
        empty_count = 0
            
        if line.startswith("CAM:BEGIN"):
            print("📥 Recibiendo imagen (Base64)...")
            capturing = True
            base64_data = ""
            continue
            
        if line == "CAM:END":
            print("✅ Transmisión finalizada.")
            break
            
        if capturing:
            base64_data += line

    ser.close()

    if not base64_data:
        print("⚠️ No se recibieron datos de imagen. Revisa la conexión con la cámara.")
        return

    # 3. Decodificar y Guardar
    try:
        # Decodificar Base64 a bytes puros
        image_bytes = base64.b64decode(base64_data)
        print(f"📦 Decodificados {len(image_bytes)} bytes.")
        
        # Verificar tamaño (QQVGA Escala de grises = 160 * 120 = 19200 bytes)
        if len(image_bytes) == WIDTH * HEIGHT:
            # Crear imagen desde los bytes puros en modo 'L' (Luminancia/escala de grises)
            img = Image.frombytes('L', (WIDTH, HEIGHT), image_bytes)
            
            # Guardar archivo
            filename = f"captura_{int(time.time())}.png"
            img.save(filename)
            print(f"🎉 Imagen guardada con éxito como '{filename}'")
        else:
            print(f"⚠️ Error: La cantidad de bytes recibida ({len(image_bytes)}) no coincide con 160x120 ({WIDTH*HEIGHT}).")
    except Exception as e:
        print(f"❌ Error procesando la imagen: {e}")

if __name__ == "__main__":
    save_image()