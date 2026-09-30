# Demon Hunter Last Ride: pruebas de escritorio

## Activación y controles

Abrir `Assets/Scenes/JapanDemonHunter.unity`. El GameObject `DesktopDebug` ya tiene `DesktopDebugMode` con **Desktop Debug Mode** activado. Entrar en Play Mode y enfocar **Game View**. No requiere visor ni Meta XR Simulator.

| Control | Acción |
| --- | --- |
| Mouse | Mirar 360°, con pitch limitado a ±80° |
| WASD / flechas | Mover solamente el observador simulado dentro de la carreta |
| Space / Shift izquierdo | Solicitar un latigazo de prueba mediante `ICartAccelerationRequester.RequestAcceleration()` |
| Z | Zombie delante |
| X / C | Zombie delante, desplazado hacia izquierda / derecha |
| V | Grupo disperso de cuatro zombies, respetando validación y límite del spawner |
| Click izquierdo | Recuperar el mouse y ejecutar un swing visible |
| F1 | Mostrar/ocultar HUD |
| Esc | Liberar mouse |
| R | Reiniciar la escena de prueba, también después de la derrota |

La velocidad real del proyecto está limitada a **8 m/s**, con arranque en **3,5 m/s** y ganancia de **1,5 m/s** por latigazo. Los ejemplos de 18–20 m/s no son el balance actual. El HUD muestra velocidad base y efectiva: la carga y los impactos existentes pueden reducir la segunda y aplicar otras penalizaciones.

En Scene View, activar **Gizmos** y seleccionar `DesktopDebug`. Se dibujan los cinco puntos terrestres con nombre y dirección; verde indica capacidad disponible, rojo ocupado. También se dibujan el CapsuleCollider de la espada y la esfera del barrido de `SwordDamage`; cambian de cian a rojo durante una ventana de ataque. No se crean marcadores permanentes ni luces de debug.

Para repetir la instalación en una escena principal que ya existe: `Tools > Game > Setup Desktop Debug (in place)`. No reconstruye la escena ni cambia los valores de un componente de debug existente.

## Desactivar completamente

Detener Play Mode, desmarcar **Desktop Debug Mode** en `DesktopDebug` y guardar la escena. También puede deshabilitarse el componente o el GameObject. La siguiente entrada en Play utiliza el arranque XR habitual.

Desactivar el checkbox durante Play retira la cámara de escritorio, devuelve la espada a su padre/pose originales y restaura los estados habilitados de los componentes suspendidos. Para volver a iniciar XR hay que detener y volver a entrar en Play: XR se inicializa al comenzar la sesión.

El comportamiento del harness, sus objetos temporales, controles, logs, HUD y Gizmos están bajo `UNITY_EDITOR`. El script de preparación pertenece al ensamblado Editor. No se ejecutan en la build de Quest. No se compiló ninguna build.

## Archivos y GameObjects

Nuevos scripts de producto/tooling:

- `Assets/Scripts/Gameplay/DesktopDebugMode.cs`: capa de prueba y restauración.
- `Assets/Scripts/Gameplay/Editor/DesktopDebugSetup.cs`: instalación idempotente y sesión XR temporal.

Scripts existentes modificados en esta ronda:

- `Assets/Scripts/Monsters/PrototypeSwordController.cs`: configuración pública del input y poses de swing; lectura de `IsSwinging`. Su comportamiento original permanece como valor por defecto.
- `Assets/Scripts/Monsters/MonsterSpawner.cs`: dos entradas exclusivas del Editor, `TrySpawnAtAngle` y `NotifyDebugFirstGallop`, que reutilizan sus propias validaciones, instanciación, contexto y puerta de inicio.

Escena modificada: `Assets/Scenes/JapanDemonHunter.unity`, únicamente para instalar `DesktopDebug` en esta ronda. Las modificaciones de las siete features anteriores se conservan y están documentadas en `Docs/Requested-VR-Features-2026-09-29.md`.

Objetos creados solo durante Play: `DesktopDebug_ViewOrigin`, `DesktopDebug_Hunter` y `DesktopDebug_Camera`. La cámara copia las opciones de la cámara principal y sus datos URP. El cuchillo existente se monta temporalmente en esta cámara; se suspende su controlador de agarre y se restaura al desactivar el modo. No se mueven la cámara VR ni el XR Origin.

Scripts reproducibles de comprobación en `Tools/Verification/`: `VerifyDesktopControls.cs`, `VerifyDesktopCombat.cs`, `VerifyDesktopAudioSafety.cs`, `CaptureDesktopView.cs`, `InspectDesktopSetup.cs` y los inspectores `InspectDesktopPlay.cs`, `InspectDesktopCombat.cs`, `InspectDesktopAudio.cs`. Están fuera de `Assets` y no forman parte del jugador.

## Parámetros del Inspector

- Activación: `desktopDebugMode`.
- Vista: `eyeHeight` (1,6 m sobre el origen del rig), `mouseSensitivity`, `cameraMoveSpeed`, `cameraHalfExtents`.
- Spawn: `zombieSpawnRadius` (15 m), `lateralSpawnAngle` (12°), `groupSpawnCount` (4).
- Espada: `swordViewOffset`, `swingStartEuler`, `swingEndEuler`; daño y duración siguen perteneciendo a los componentes existentes.
- Diagnóstico: `showHud`, `showGizmos`, `logEvents`.

## Integración y audio

Se reutilizan `SmoothFollowCamera`, `SimulatedHunterMovement`, `PrototypeSwordController`, `SwordDamage`, `SwordSwingAudio`, `MonsterSpawner`, la FSM, navegación, reservas, animaciones y audio de impacto existentes. No se agregó un sistema alternativo de daño, aceleración o enemigos.

`Space` llama a la misma interfaz y función que `WhipHandle.FireLash`. Como esa función pública no emite el evento de gesto de las riendas, el harness abre la espera del spawner mediante su ruta de inicio existente después de la primera solicitud de debug.

La colección existente contiene `1.wav`, `2.wav`, `3.wav`, `4.wav`, `5.wav` en `Assets/Art/Audio/swordSound`. Son las extracciones del audio de `1.mp4`…`5.mp4` de la ronda anterior; los originales permanecen. Esta ronda no crea ni descarga sonidos.

Cada swing abre la misma ventana explícita de `SwordDamage`. `SwordSwingAudio` selecciona aleatoriamente un clip válido, excluyendo el último cuando hay otro disponible, y ejecuta `PlayOneShot` en el AudioSource existente. `PrototypeSwordController.IsSwinging` impide iniciar otro swing mientras el actual sigue abierto; `SwordDamage` registra las víctimas por ventana. Las ventanas automáticas por velocidad se suspenden temporalmente durante este control explícito y se restauran al salir, para que volver a la pose de reposo no produzca un segundo sonido.

La opción de escritorio omite el arranque XR mediante una **copia temporal en memoria** de `XRGeneralSettings`. El asset original y los loaders/features no se editan ni guardan. Al terminar Play se restaura la instancia original. No se cambian OpenXR, XR Interaction Toolkit, HandsOnly, Quest ni el manifest. Se permite `runInBackground` solo durante debug y se restaura al desactivarlo.

## Pruebas realmente ejecutadas

Se ejecutó la escena principal en Play Mode, sin visor, con eventos de mouse/teclado de Input System. Las pruebas aíslan temporalmente dispositivos y política de foco para inyectar input sin depender de la ventana del terminal; los restauran al terminar.

| Verificación | Resultado |
| --- | --- |
| Vista inicial | Orientada hacia los caballos; ojos a 1,6 m sobre el origen; pose local del rig intacta |
| Mouse, WASD, F1 | Giro de 360°, desplazamiento local y toggle de HUD verificados |
| Latigazo/coasting | Velocidad 3,204 → 4,652; al soltar 4,652 → 4,559 → 4,478 → 4,385; segundo latigazo 5,831 m/s |
| Spawn Z/X/C/V | Frente e izquierda/derecha comprobados; V agregó 4, usando el spawner real |
| Zombie rebasado | Zombies siguieron en `ChaseAttachment` después de pasar de z local negativa a positiva |
| Agarre | `SelectAttachment → ChaseAttachment → Attach → Attached`; puntos exclusivos, seguimiento de carreta y ciclo de ataque existente |
| Espada | Swing visible con click; daño real 30 → 18 → 6 → 0; reacción visual y estado Dead |
| Audio | Última corrida: **12 swings**, secuencia **2,1,3,4,1,2,3,2,3,2,1,5**, todos los clips y sin repetición inmediata |
| Duplicados/reposo | Un sonido por swing, un daño por víctima y ventana; sin audio continuo en reposo |
| Clips faltantes | Un clip válido entre nulos, array vacío y array nulo: sin errores; colección original restaurada |
| AudioSource | Uno solo; `PlayOneShot` comenzó; señal de fuente RMS 0,059; Editor sin mute; ganancia del one-shot 0,35 |
| Desactivación | Cámara temporal retirada, arma y estados de componentes VR restaurados |
| EditMode enfocado | **4/4** `SwordSwingAudioTests` aprobados; no es una nueva corrida de toda la suite |

Evidencia: `Docs/Verification/DesktopControls-2026-09-29.json`, `DesktopCombat-2026-09-29.json`, `DesktopAudioSafety-2026-09-29.json` y `DesktopDebugGameView.png`.

Las comprobaciones externas pueden ejecutarse desde la raíz con `unity command run_script --file Tools/Verification/VerifyDesktopControls.cs --entry VerifyDesktopControls.Run --timeout_ms 20000`, y equivalentes para Combat y AudioSafety, estando en Play. Combat reinicia la escena para empezar una prueba limpia. El runner de audio enfocado usa `unity command run_tests --mode EditMode --filter SwordSwingAudioTests --async_tests true` fuera de Play.

## Problemas y límites

- La pérdida de foco pausaba el juego y liberaba el cursor. El harness continúa en segundo plano y el click vuelve a capturarlo; las pruebas restauran sus cambios temporales de input.
- El canal del Editor se desconectó al recompilar y el Editor se cerró durante la sesión; se retomó en el Editor abierto nuevamente. Los resultados finales vienen de corridas completadas, no de los intentos interrumpidos.
- Los scripts externos emitieron advertencias de referencia de `Unity.Plastic.Newtonsoft.Json`; las comprobaciones finales compilaron y ejecutaron. No se introdujeron errores de compilación en los scripts de Assets.
- Se observó la iluminación existente en Game View, sin ajustes adicionales. Hay captura con HUD. La legibilidad estética requiere revisión humana de esa vista.
- La escena conserva la derrota y las penalizaciones existentes; usar R para reiniciar una prueba. No se agregó invulnerabilidad ni se deshabilitó el gigante.
- Quest 2 sigue siendo necesario para calibración real de ojos/suelo, alcance de brazos, selección y agarre con manos, gestos reales de latigazo, confort espacial/volumen y rendimiento en hardware. La señal de audio se comprobó en PC; no se certificó el confort auditivo en Quest.
- `CarriageMotor.RequestAcceleration`, fuerza/cooldown/detección del látigo, `WhipHandle` y `WhipLashModel` no se modificaron en esta ronda. No se hicieron commits ni builds.
