#include <Servo.h>

const int SERVO_COUNT = 2;
const int SERVO_PINS[SERVO_COUNT] = { 9, 10 };

const int MIN_ANGLE   = 0;
const int MAX_ANGLE   = 90;
const int SWEEP_DELAY = 15;
const int STRESS_HOLD = 600;
const int STRESS_LOOPS = 5;

Servo servos[SERVO_COUNT];
String inBuf = "";

void setup() {
  Serial.begin(9600);
  for (int i = 0; i < SERVO_COUNT; i++) {
    servos[i].attach(SERVO_PINS[i]);
    servos[i].write(MIN_ANGLE);
  }
// 55 grados es lo maximos
  Serial.println("=== Servo Test (dual) ===");
  Serial.println("Servo 1 -> pin 9   |   Servo 2 -> pin 10");
  Serial.println();
  Serial.println("Comandos:");
  Serial.println("  1:<num>     -> servo 1 a ese angulo");
  Serial.println("  2:<num>     -> servo 2 a ese angulo");
  Serial.println("  both:<num>  -> ambos servos al mismo angulo (simultaneo)");
  Serial.println("  open        -> ambos a MAX_ANGLE");
  Serial.println("  close       -> ambos a MIN_ANGLE");
  Serial.println("  sweep1      -> sweep solo servo 1");
  Serial.println("  sweep2      -> sweep solo servo 2");
  Serial.println("  sweepall    -> sweep ambos en paralelo");
  Serial.println("  stress      -> abre y cierra ambos en simultaneo X veces");
  Serial.println();
  Serial.println("Test inicial: sweep en paralelo...");
  doParallelSweep();
  Serial.println("Listo. Esperando comandos.");
}

void loop() {
  while (Serial.available()) {
    char c = (char)Serial.read();
    if (c == '\n') {
      handleCommand(inBuf);
      inBuf = "";
    } else if (c != '\r') {
      inBuf += c;
    }
  }
}

void handleCommand(String cmd) {
  cmd.trim();
  if (cmd.length() == 0) return;

  if (cmd.equalsIgnoreCase("open")) {
    moveAll(MAX_ANGLE);
    return;
  }
  if (cmd.equalsIgnoreCase("close")) {
    moveAll(MIN_ANGLE);
    return;
  }
  if (cmd.equalsIgnoreCase("sweep1")) {
    doSingleSweep(0);
    return;
  }
  if (cmd.equalsIgnoreCase("sweep2")) {
    doSingleSweep(1);
    return;
  }
  if (cmd.equalsIgnoreCase("sweepall")) {
    doParallelSweep();
    return;
  }
  if (cmd.equalsIgnoreCase("stress")) {
    doStressTest();
    return;
  }

  int sep = cmd.indexOf(':');
  if (sep < 0) {
    Serial.print("Comando invalido: ");
    Serial.println(cmd);
    return;
  }

  String target = cmd.substring(0, sep);
  int angle = cmd.substring(sep + 1).toInt();

  if (target == "1") {
    moveOne(0, angle);
  } else if (target == "2") {
    moveOne(1, angle);
  } else if (target.equalsIgnoreCase("both")) {
    moveAll(angle);
  } else {
    Serial.print("Target desconocido: ");
    Serial.println(target);
  }
}

void moveOne(int idx, int angle) {
  angle = clampAngle(angle);
  servos[idx].write(angle);
  Serial.print("Servo ");
  Serial.print(idx + 1);
  Serial.print(" -> ");
  Serial.print(angle);
  Serial.println(" grados");
}

void moveAll(int angle) {
  angle = clampAngle(angle);
  for (int i = 0; i < SERVO_COUNT; i++) {
    servos[i].write(angle);
  }
  Serial.print("Ambos servos -> ");
  Serial.print(angle);
  Serial.println(" grados (simultaneo)");
}

int clampAngle(int angle) {
  if (angle < 0)   return 0;
  if (angle > 180) return 180;
  return angle;
}

void doSingleSweep(int idx) {
  Serial.print("Sweep servo ");
  Serial.println(idx + 1);
  for (int a = MIN_ANGLE; a <= MAX_ANGLE; a++) {
    servos[idx].write(a);
    delay(SWEEP_DELAY);
  }
  for (int a = MAX_ANGLE; a >= MIN_ANGLE; a--) {
    servos[idx].write(a);
    delay(SWEEP_DELAY);
  }
  Serial.println("Sweep individual completado.");
}

void doParallelSweep() {
  Serial.println("Sweep en paralelo (ambos servos a la vez)...");
  for (int a = MIN_ANGLE; a <= MAX_ANGLE; a++) {
    for (int i = 0; i < SERVO_COUNT; i++) servos[i].write(a);
    delay(SWEEP_DELAY);
  }
  for (int a = MAX_ANGLE; a >= MIN_ANGLE; a--) {
    for (int i = 0; i < SERVO_COUNT; i++) servos[i].write(a);
    delay(SWEEP_DELAY);
  }
  Serial.println("Sweep paralelo completado.");
}

void doStressTest() {
  Serial.print("Stress test: ");
  Serial.print(STRESS_LOOPS);
  Serial.println(" ciclos abrir/cerrar simultaneo.");
  for (int n = 0; n < STRESS_LOOPS; n++) {
    Serial.print("  Ciclo ");
    Serial.print(n + 1);
    Serial.print("/");
    Serial.println(STRESS_LOOPS);
    for (int i = 0; i < SERVO_COUNT; i++) servos[i].write(MAX_ANGLE);
    delay(STRESS_HOLD);
    for (int i = 0; i < SERVO_COUNT; i++) servos[i].write(MIN_ANGLE);
    delay(STRESS_HOLD);
  }
  Serial.println("Stress test completado.");
}
