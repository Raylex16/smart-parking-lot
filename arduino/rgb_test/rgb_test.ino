// ============================================================
// rgb_test.ino  —  Test de LED RGB con modo discoteca
// Compatible con Arduino Uno, Nano y Mega 2560
// ============================================================
//
// CABLEADO — LED RGB de cátodo común (el más habitual):
//
//   LED RGB                       Arduino
//   ┌─────────┐
//   │  R  ────┼── R 220Ω ──── Pin 9   (PWM)
//   │  GND ───┼───────────── GND
//   │  G  ────┼── R 220Ω ──── Pin 10  (PWM)
//   │  B  ────┼── R 220Ω ──── Pin 11  (PWM)
//   └─────────┘
//
//   Pata más larga = GND (cátodo común).
//   Orden típico de patas: R · GND · G · B  (izquierda a derecha de frente).
//
// CABLEADO — LED RGB de ánodo común (pata larga = VCC):
//
//   LED RGB                       Arduino
//   ┌─────────┐
//   │  R  ────┼── R 220Ω ──── Pin 9
//   │  VCC ───┼───────────── 5V
//   │  G  ────┼── R 220Ω ──── Pin 10
//   │  B  ────┼── R 220Ω ──── Pin 11
//   └─────────┘
//
//   Para ánodo común cambiar: #define ANODO_COMUN 1  (línea ~25)
//   En ánodo común la lógica se invierte: 0 = encendido, 255 = apagado.
//
// HERRAMIENTA
//   Arduino IDE → Monitor Serial 9600 baud, fin de línea: "Nueva línea"
//
// ════════════════════════════════════════════════════════════
//  COMANDOS DISPONIBLES
// ════════════════════════════════════════════════════════════
//   r:<0-255>          — canal rojo
//   g:<0-255>          — canal verde
//   b:<0-255>          — canal azul
//   rgb:<R>,<G>,<B>    — los tres canales a la vez  (ej. rgb:255,128,0)
//   hex:<RRGGBB>       — color en hexadecimal       (ej. hex:FF8C00)
//   bright:<0-100>     — brillo global en %         (ej. bright:50)
//   fade:<color>       — transición suave hacia un color
//   blink:<n>          — parpadea N veces con el color actual
//   ─── colores predefinidos ───
//   red / green / blue / white / yellow / cyan / magenta
//   orange / purple / pink / warm / off
//   ─── modos automáticos ───
//   disco              — modo discoteca: colores aleatorios al ritmo
//   rainbow            — ciclo arco iris continuo
//   strobe             — estrobo blanco rápido
//   beat:<BPM>         — flashes al BPM indicado  (ej. beat:120)
//   candle             — parpadeo de vela (amarillo cálido)
//   stop               — detiene cualquier modo automático
//   status             — muestra los valores actuales

// ─── Configuración ────────────────────────────────────────
#define ANODO_COMUN   0    // 0 = cátodo común (habitual)  |  1 = ánodo común

#define PIN_R         9
#define PIN_G         10
#define PIN_B         11

// ─── Modos de operación ───────────────────────────────────
enum Modo {
    MODO_MANUAL = 0,
    MODO_DISCO,
    MODO_RAINBOW,
    MODO_STROBE,
    MODO_BEAT,
    MODO_CANDLE
};

// ─── Estado global ────────────────────────────────────────
static uint8_t  curR = 0, curG = 0, curB = 0;
static uint8_t  brightness = 100;   // porcentaje 0-100
static Modo     modo        = MODO_MANUAL;
static uint16_t beatBPM     = 120;
static String   inputBuf    = "";

// ─── Escritura con ajuste de brillo y polaridad ──────────
void writeRGB(uint8_t r, uint8_t g, uint8_t b) {
    // Aplicar brillo global
    r = (uint16_t)r * brightness / 100;
    g = (uint16_t)g * brightness / 100;
    b = (uint16_t)b * brightness / 100;

#if ANODO_COMUN
    analogWrite(PIN_R, 255 - r);
    analogWrite(PIN_G, 255 - g);
    analogWrite(PIN_B, 255 - b);
#else
    analogWrite(PIN_R, r);
    analogWrite(PIN_G, g);
    analogWrite(PIN_B, b);
#endif
}

void applyCurrentColor() {
    writeRGB(curR, curG, curB);
}

void setColor(uint8_t r, uint8_t g, uint8_t b) {
    curR = r; curG = g; curB = b;
    applyCurrentColor();
}

void off() { writeRGB(0, 0, 0); }

// ─── Colores predefinidos ─────────────────────────────────
struct Color { uint8_t r, g, b; const char* nombre; };

static const Color COLORS[] PROGMEM = {
    { 255,   0,   0, "red"     },
    {   0, 255,   0, "green"   },
    {   0,   0, 255, "blue"    },
    { 255, 255, 255, "white"   },
    { 255, 255,   0, "yellow"  },
    {   0, 255, 255, "cyan"    },
    { 255,   0, 255, "magenta" },
    { 255, 100,   0, "orange"  },
    { 128,   0, 255, "purple"  },
    { 255,  20, 147, "pink"    },
    { 255, 180,  50, "warm"    },
    {   0,   0,   0, "off"     },
};
static const uint8_t COLOR_COUNT = sizeof(COLORS) / sizeof(COLORS[0]);

// Devuelve true si el nombre coincide con un color predefinido
bool resolveNamedColor(const String& name, uint8_t& r, uint8_t& g, uint8_t& b) {
    for (uint8_t i = 0; i < COLOR_COUNT; i++) {
        char buf[12];
        strcpy_P(buf, (const char*)pgm_read_ptr(&COLORS[i].nombre));
        if (name.equalsIgnoreCase(buf)) {
            r = pgm_read_byte(&COLORS[i].r);
            g = pgm_read_byte(&COLORS[i].g);
            b = pgm_read_byte(&COLORS[i].b);
            return true;
        }
    }
    return false;
}

// ─── Fade suave entre dos colores ─────────────────────────
void fadeToRGB(uint8_t tr, uint8_t tg, uint8_t tb, uint16_t durMs = 500) {
    uint8_t fr = curR, fg = curG, fb = curB;
    const uint8_t STEPS = 50;
    uint16_t stepDelay = durMs / STEPS;
    for (uint8_t i = 0; i <= STEPS; i++) {
        uint8_t r = fr + (int16_t)(tr - fr) * i / STEPS;
        uint8_t g = fg + (int16_t)(tg - fg) * i / STEPS;
        uint8_t b = fb + (int16_t)(tb - fb) * i / STEPS;
        writeRGB(r, g, b);
        delay(stepDelay);
        // Si llega un comando mientras hace fade, abortar
        if (Serial.available()) break;
    }
    curR = tr; curG = tg; curB = tb;
    applyCurrentColor();
}

// ─── Blink ────────────────────────────────────────────────
void doBlink(uint8_t times, uint16_t onMs = 200, uint16_t offMs = 200) {
    for (uint8_t i = 0; i < times; i++) {
        applyCurrentColor();
        delay(onMs);
        off();
        delay(offMs);
    }
    applyCurrentColor();
}

// ─── Modo DISCO ───────────────────────────────────────────
//
// Alterna entre sub-efectos cada 3-8 segundos:
//   1. ColorFlash   — flashes de colores aleatorios cortos
//   2. RainbowBurst — arco iris acelerado
//   3. Strobe       — estrobo en color aleatorio
//   4. DoubleFlash  — dos flashes rápidos + pausa
//   5. ColorWash    — fade lento entre colores saturados
//
// Cada sub-efecto dura entre 2 y 6 segundos antes de cambiar.

static uint8_t  discoSubMode   = 0;
static uint32_t discoSubUntil  = 0;
static uint32_t discoNextFlash = 0;
static uint16_t discoFlashIdx  = 0;
static uint8_t  discoPhase     = 0;

// Genera un color vivo aleatorio (siempre saturado, nunca gris)
void randomVividColor(uint8_t& r, uint8_t& g, uint8_t& b) {
    uint8_t which = random(0, 6);
    switch (which) {
        case 0: r=255; g=random(0,80);  b=0;           break; // rojo-naranja
        case 1: r=255; g=0;             b=random(0,80); break; // rojo-violeta
        case 2: r=0;   g=255;           b=random(0,80); break; // verde
        case 3: r=0;   g=random(0,80);  b=255;          break; // azul
        case 4: r=255; g=0;             b=255;           break; // magenta
        case 5: r=random(0,80); g=255;  b=random(0,80); break; // verde-teal
    }
}

void discoTick() {
    uint32_t now = millis();

    // Cambiar sub-modo si toca
    if (now >= discoSubUntil) {
        discoSubMode  = random(0, 5);
        discoSubUntil = now + random(2000, 6000);
        discoPhase    = 0;

        Serial.print(F("  [DISCO] sub-modo: "));
        const char* names[] = {"ColorFlash","RainbowBurst","Strobe","DoubleFlash","ColorWash"};
        Serial.println(names[discoSubMode]);
    }

    uint8_t r, g, b;

    switch (discoSubMode) {

        case 0: // ColorFlash — flash corto de color aleatorio cada 80-300 ms
            if (now >= discoNextFlash) {
                randomVividColor(r, g, b);
                writeRGB(r, g, b);
                delay(random(40, 120));
                off();
                discoNextFlash = now + random(80, 300);
            }
            break;

        case 1: // RainbowBurst — arco iris rápido
        {
            uint16_t h = (discoFlashIdx++) % 360;
            // HSV → RGB (S=1, V=1)
            uint8_t region = h / 60;
            uint8_t rem = (h % 60) * 255 / 60;
            switch (region) {
                case 0: r=255;    g=rem;    b=0;       break;
                case 1: r=255-rem;g=255;    b=0;       break;
                case 2: r=0;      g=255;    b=rem;     break;
                case 3: r=0;      g=255-rem;b=255;     break;
                case 4: r=rem;    g=0;      b=255;     break;
                default:r=255;    g=0;      b=255-rem; break;
            }
            writeRGB(r, g, b);
            delay(5);
        }
            break;

        case 2: // Strobe — estrobo blanco muy rápido
            writeRGB(255, 255, 255);
            delay(25);
            off();
            delay(25);
            break;

        case 3: // DoubleFlash — dos pulsos rápidos + pausa larga
            if (now >= discoNextFlash) {
                randomVividColor(r, g, b);
                writeRGB(r, g, b); delay(60);
                off();             delay(60);
                writeRGB(r, g, b); delay(60);
                off();
                discoNextFlash = now + random(300, 700);
            }
            break;

        case 4: // ColorWash — transición suave entre colores vivos
            if (now >= discoNextFlash) {
                randomVividColor(r, g, b);
                fadeToRGB(r, g, b, random(400, 900));
                discoNextFlash = millis() + random(200, 600);
            }
            break;
    }
}

// ─── Modo RAINBOW ─────────────────────────────────────────
static uint16_t rainbowHue = 0;

void rainbowTick() {
    uint16_t h = rainbowHue;
    rainbowHue = (rainbowHue + 1) % 360;
    uint8_t r, g, b;
    uint8_t region = h / 60;
    uint8_t rem = (h % 60) * 255 / 60;
    switch (region) {
        case 0: r=255;    g=rem;    b=0;       break;
        case 1: r=255-rem;g=255;    b=0;       break;
        case 2: r=0;      g=255;    b=rem;     break;
        case 3: r=0;      g=255-rem;b=255;     break;
        case 4: r=rem;    g=0;      b=255;     break;
        default:r=255;    g=0;      b=255-rem; break;
    }
    writeRGB(r, g, b);
    delay(8);
}

// ─── Modo STROBE ──────────────────────────────────────────
void strobeTick() {
    writeRGB(curR ? curR : 255, curG ? curG : 255, curB ? curB : 255);
    delay(20);
    off();
    delay(20);
}

// ─── Modo BEAT ────────────────────────────────────────────
static uint32_t beatNextMs = 0;
static uint8_t  beatFlashPhase = 0;

void beatTick() {
    uint32_t now = millis();
    uint32_t interval = 60000UL / beatBPM;  // ms entre beats

    if (now >= beatNextMs) {
        uint8_t r, g, b;
        randomVividColor(r, g, b);
        writeRGB(r, g, b);
        delay(40);
        off();
        beatNextMs = now + interval;
    }
}

// ─── Modo CANDLE ──────────────────────────────────────────
void candleTick() {
    // Amarillo cálido con parpadeo aleatorio (simula llama)
    uint8_t flicker = random(160, 255);
    uint8_t r = flicker;
    uint8_t g = (uint16_t)flicker * 80 / 255;
    uint8_t b = 0;
    writeRGB(r, g, b);
    delay(random(30, 100));
}

// ─── Parseo de comandos ───────────────────────────────────
void printStatus() {
    Serial.println(F("=== Estado actual ==="));
    Serial.print(F("  Color:      R=")); Serial.print(curR);
    Serial.print(F(" G="));             Serial.print(curG);
    Serial.print(F(" B="));             Serial.println(curB);
    Serial.print(F("  Hex:        #"));
    if (curR < 16) Serial.print('0'); Serial.print(curR, HEX);
    if (curG < 16) Serial.print('0'); Serial.print(curG, HEX);
    if (curB < 16) Serial.print('0'); Serial.println(curB, HEX);
    Serial.print(F("  Brillo:     ")); Serial.print(brightness); Serial.println(F("%"));
    Serial.print(F("  Modo:       "));
    switch (modo) {
        case MODO_MANUAL:  Serial.println(F("manual"));  break;
        case MODO_DISCO:   Serial.println(F("disco"));   break;
        case MODO_RAINBOW: Serial.println(F("rainbow")); break;
        case MODO_STROBE:  Serial.println(F("strobe"));  break;
        case MODO_BEAT:
            Serial.print(F("beat (")); Serial.print(beatBPM); Serial.println(F(" BPM)"));
            break;
        case MODO_CANDLE:  Serial.println(F("candle"));  break;
    }
    Serial.print(F("  Polaridad:  "));
    Serial.println(ANODO_COMUN ? F("ánodo común") : F("cátodo común"));
}

void handleCommand(const String& cmd) {
    // ── stop ──────────────────────────────────────────────
    if (cmd.equalsIgnoreCase("stop")) {
        modo = MODO_MANUAL;
        applyCurrentColor();
        Serial.println(F("Modo manual. Color restaurado."));
        return;
    }

    // ── status ────────────────────────────────────────────
    if (cmd.equalsIgnoreCase("status")) {
        printStatus();
        return;
    }

    // ── modos automáticos ─────────────────────────────────
    if (cmd.equalsIgnoreCase("disco")) {
        modo = MODO_DISCO;
        discoSubUntil = 0;    // forzar cambio inmediato de sub-modo
        discoNextFlash = 0;
        randomSeed(analogRead(A0));  // semilla aleatoria por ruido analógico
        Serial.println(F("Modo DISCO activado. Escribe 'stop' para salir."));
        return;
    }
    if (cmd.equalsIgnoreCase("rainbow")) {
        modo = MODO_RAINBOW;
        rainbowHue = 0;
        Serial.println(F("Modo RAINBOW activado. Escribe 'stop' para salir."));
        return;
    }
    if (cmd.equalsIgnoreCase("strobe")) {
        modo = MODO_STROBE;
        Serial.println(F("Modo STROBE activado. Escribe 'stop' para salir."));
        return;
    }
    if (cmd.equalsIgnoreCase("candle")) {
        modo = MODO_CANDLE;
        Serial.println(F("Modo CANDLE activado. Escribe 'stop' para salir."));
        return;
    }

    // ── beat:<BPM> ────────────────────────────────────────
    if (cmd.startsWith(F("beat:"))) {
        int bpm = cmd.substring(5).toInt();
        if (bpm < 20 || bpm > 300) {
            Serial.println(F("BPM fuera de rango. Usa entre 20 y 300."));
            return;
        }
        beatBPM = bpm;
        beatNextMs = 0;
        modo = MODO_BEAT;
        Serial.print(F("Modo BEAT activado: "));
        Serial.print(beatBPM);
        Serial.println(F(" BPM. Escribe 'stop' para salir."));
        return;
    }

    // ── Comandos manuales (cambian a modo MANUAL) ─────────
    modo = MODO_MANUAL;

    // r:<0-255>
    if (cmd.startsWith(F("r:"))) {
        curR = constrain(cmd.substring(2).toInt(), 0, 255);
        applyCurrentColor();
        Serial.print(F("R = ")); Serial.println(curR);
        return;
    }

    // g:<0-255>
    if (cmd.startsWith(F("g:"))) {
        curG = constrain(cmd.substring(2).toInt(), 0, 255);
        applyCurrentColor();
        Serial.print(F("G = ")); Serial.println(curG);
        return;
    }

    // b:<0-255>
    if (cmd.startsWith(F("b:"))) {
        curB = constrain(cmd.substring(2).toInt(), 0, 255);
        applyCurrentColor();
        Serial.print(F("B = ")); Serial.println(curB);
        return;
    }

    // rgb:<R>,<G>,<B>
    if (cmd.startsWith(F("rgb:"))) {
        String vals = cmd.substring(4);
        int c1 = vals.indexOf(',');
        int c2 = vals.lastIndexOf(',');
        if (c1 < 0 || c1 == c2) {
            Serial.println(F("Formato: rgb:<R>,<G>,<B>  ej. rgb:255,128,0"));
            return;
        }
        curR = constrain(vals.substring(0, c1).toInt(), 0, 255);
        curG = constrain(vals.substring(c1 + 1, c2).toInt(), 0, 255);
        curB = constrain(vals.substring(c2 + 1).toInt(), 0, 255);
        applyCurrentColor();
        Serial.print(F("RGB = "));
        Serial.print(curR); Serial.print(F(","));
        Serial.print(curG); Serial.print(F(","));
        Serial.println(curB);
        return;
    }

    // hex:<RRGGBB>
    if (cmd.startsWith(F("hex:"))) {
        String h = cmd.substring(4);
        if (h.length() != 6) {
            Serial.println(F("Formato: hex:<RRGGBB>  ej. hex:FF8C00"));
            return;
        }
        curR = (uint8_t)strtol(h.substring(0, 2).c_str(), nullptr, 16);
        curG = (uint8_t)strtol(h.substring(2, 4).c_str(), nullptr, 16);
        curB = (uint8_t)strtol(h.substring(4, 6).c_str(), nullptr, 16);
        applyCurrentColor();
        Serial.print(F("Color #")); Serial.println(h);
        return;
    }

    // bright:<0-100>
    if (cmd.startsWith(F("bright:"))) {
        brightness = constrain(cmd.substring(7).toInt(), 0, 100);
        applyCurrentColor();
        Serial.print(F("Brillo: ")); Serial.print(brightness); Serial.println(F("%"));
        return;
    }

    // blink:<n>
    if (cmd.startsWith(F("blink:"))) {
        uint8_t n = constrain(cmd.substring(6).toInt(), 1, 20);
        doBlink(n);
        Serial.print(F("Blink x")); Serial.println(n);
        return;
    }

    // fade:<color>
    if (cmd.startsWith(F("fade:"))) {
        String colorName = cmd.substring(5);
        uint8_t r, g, b;
        if (resolveNamedColor(colorName, r, g, b)) {
            fadeToRGB(r, g, b, 600);
            Serial.print(F("Fade → ")); Serial.println(colorName);
        } else {
            Serial.print(F("Color desconocido: ")); Serial.println(colorName);
        }
        return;
    }

    // ── Colores predefinidos directos ─────────────────────
    uint8_t r, g, b;
    if (resolveNamedColor(cmd, r, g, b)) {
        setColor(r, g, b);
        Serial.print(F("Color: ")); Serial.println(cmd);
        return;
    }

    // ── help ──────────────────────────────────────────────
    if (cmd.equalsIgnoreCase("help")) {
        Serial.println(F(""));
        Serial.println(F("=== rgb_test.ino — Comandos ==="));
        Serial.println(F("  r:<0-255>         canal rojo"));
        Serial.println(F("  g:<0-255>         canal verde"));
        Serial.println(F("  b:<0-255>         canal azul"));
        Serial.println(F("  rgb:<R>,<G>,<B>   tres canales  (ej. rgb:255,128,0)"));
        Serial.println(F("  hex:<RRGGBB>      color hex     (ej. hex:FF8C00)"));
        Serial.println(F("  bright:<0-100>    brillo global en %"));
        Serial.println(F("  fade:<color>      transición suave hacia ese color"));
        Serial.println(F("  blink:<n>         parpadea N veces"));
        Serial.println(F("  --- colores predefinidos ---"));
        Serial.println(F("  red  green  blue  white  yellow  cyan  magenta"));
        Serial.println(F("  orange  purple  pink  warm  off"));
        Serial.println(F("  --- modos automáticos ---"));
        Serial.println(F("  disco             colores aleatorios (5 sub-efectos)"));
        Serial.println(F("  rainbow           ciclo arco iris continuo"));
        Serial.println(F("  strobe            estrobo blanco rápido"));
        Serial.println(F("  beat:<BPM>        flashes al ritmo  (ej. beat:120)"));
        Serial.println(F("  candle            parpadeo de vela"));
        Serial.println(F("  stop              detiene modo automático"));
        Serial.println(F("  status            valores actuales"));
        return;
    }

    Serial.print(F("Desconocido: ")); Serial.println(cmd);
    Serial.println(F("Escribe 'help' para ver todos los comandos."));
}

// ─── Setup ────────────────────────────────────────────────
void setup() {
    Serial.begin(9600);
    while (!Serial) {}

    pinMode(PIN_R, OUTPUT);
    pinMode(PIN_G, OUTPUT);
    pinMode(PIN_B, OUTPUT);
    off();

    randomSeed(analogRead(A0));

    Serial.println(F(""));
    Serial.println(F("  ┌─────────────────────────────────────┐"));
    Serial.println(F("  │   rgb_test.ino — Smart Parking Lot  │"));
    Serial.println(F("  │   LED RGB con modo discoteca        │"));
    Serial.println(F("  └─────────────────────────────────────┘"));
    Serial.println(F(""));
    Serial.print(F("  Pines:     R → ")); Serial.print(PIN_R);
    Serial.print(F("   G → ")); Serial.print(PIN_G);
    Serial.print(F("   B → ")); Serial.println(PIN_B);
    Serial.print(F("  Polaridad: "));
    Serial.println(ANODO_COMUN ? F("ánodo común (pata larga → 5V)") : F("cátodo común (pata larga → GND)"));
    Serial.println(F(""));

    // Test de encendido: R → G → B → blanco → off
    Serial.println(F("  Test inicial: R → G → B → blanco → off"));
    setColor(255, 0, 0); delay(300);
    setColor(0, 255, 0); delay(300);
    setColor(0, 0, 255); delay(300);
    setColor(255, 255, 255); delay(300);
    setColor(0, 0, 0);
    Serial.println(F("  Test OK"));
    Serial.println(F(""));
    Serial.println(F("  Escribe 'disco' para modo discoteca o 'help' para ver todos los comandos."));
    Serial.println(F(""));
}

// ─── Loop ─────────────────────────────────────────────────
void loop() {
    // Leer comando del serial
    while (Serial.available()) {
        char c = (char)Serial.read();
        if (c == '\n') {
            inputBuf.trim();
            if (inputBuf.length() > 0) {
                handleCommand(inputBuf);
            }
            inputBuf = "";
        } else if (c != '\r') {
            inputBuf += c;
        }
    }

    // Ejecutar el modo activo
    switch (modo) {
        case MODO_DISCO:   discoTick();   break;
        case MODO_RAINBOW: rainbowTick(); break;
        case MODO_STROBE:  strobeTick();  break;
        case MODO_BEAT:    beatTick();    break;
        case MODO_CANDLE:  candleTick();  break;
        case MODO_MANUAL:  break;
    }
}
