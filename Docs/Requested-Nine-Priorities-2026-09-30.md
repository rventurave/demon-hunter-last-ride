# Demon Hunter: Last Ride — informe de las nueve prioridades

Fecha: 30/09/2026. Las nueve prioridades están implementadas y verificadas mediante pruebas de código y del Editor. La validación física, visual y de rendimiento en Meta Quest 2 sigue pendiente. No se hicieron builds, commits ni reconstrucciones de escena. AGENTS.md y los componentes Meta/XR se conservaron.

## 1. Estado de cada prioridad

| Prioridad | Resultado comprobado |
|---|---|
| 1. Independencia mango/mano | Cuatro combinaciones aceptadas con tracking válido; tracking inválido no autoriza gestos. Latigazo existente conserva 3,5 → 5 → 6,5 → 8 → 8 m/s. |
| 2. Dirección | Se habilitó el sistema existente de tres carriles curvos. Neutral, ambos sentidos, transición de 0,8 s y ausencia de repetición al mantener el jalón verificados. |
| 3. Recuperación | Soltar dentro permanece; fuera vuelve tras 1,25 s; sostener fuera nunca teletransporta. Velocidades y ventana de daño se cancelan al recuperar. |
| 4. Ubicación de espada | Reposo y recuperación coinciden en (0,55; 0,635249; −0,25), local a VehicleRoot. Distancias a los mangos: 0,999 y 1,321 m. Hitbox y escala preservadas. |
| 5. Audio de espada | Sonido 5 reservado al agarre. Aire silencioso. Daño real reproduce uno de 1–4, sin repetición inmediata ni spam por contacto persistente. |
| 6. Manos | Ambas manos válidas hacen daño; contacto suave y tracking inválido no. 8 de daño, cuatro golpes para el zombie de 30 HP. |
| 7. Muerte | zombie_muerte_Mahaha.mp3 una vez al morir; volumen efectivo 0,512 frente a 0,256 anterior. Sin daño posterior ni enganche persistente. |
| 8. Menú | Play Mode comprobó espera con motor/spawner/timer detenidos, un mango insuficiente y ambos mangos iniciando exactamente una vez. |
| 9. Game Over | Play Mode comprobó 2,4 m de distancia, orientación al espectador y rig intacto. Captura revisada en Editor. |

## 2–3. Scripts modificados y nuevos

Modificados en esta implementación:

- Assets/Scripts/Reins/ReinHandle.cs: política de mano y activación de dirección existente.
- Assets/Scripts/Reins/CarriageMotor.cs: pausa de sesión exclusivamente; integración de avance y aceleración existentes conservada.
- Assets/Scripts/Gameplay/WhipHandle.cs: valor por defecto de la restricción de mano desactivado en el sistema heredado.
- Assets/Scripts/Gameplay/GrabbableWeapon.cs: recuperación y evento de transición de agarre.
- Assets/Scripts/Gameplay/SwordSwingAudio.cs: separación de agarre/impacto y eliminación de sonido en el aire.
- Assets/Scripts/Gameplay/HandStrikeController.cs: tracking válido, cooldown y audio exclusivo del daño de manos.
- Assets/Scripts/Monsters/SwordDamage.cs: cooldown configurable de ventanas automáticas; valor cero conserva comportamiento de espada.
- Assets/Scripts/Gameplay/GameSoundscape.cs: audio de muerte y eliminación de golpe.mp3 universal en cualquier daño.
- Assets/Scripts/Gameplay/GameSessionController.cs: espera, evento de inicio y presentación inicial/final en el mundo.
- Assets/Scripts/Gameplay/DesktopDebugMode.cs: el latigazo simulado inicia la sesión en la ruta UNITY_EDITOR.
- Assets/Scripts/Gameplay/Editor/PlayableSceneCreator.cs: configuración equivalente para futuras escenas; no se ejecutó rebuild.
- Assets/Scripts/Gameplay/Editor/RequestedFeaturesSetup.cs: extracción 5 exclusiva de agarre y 1–4 para impacto.
- Assets/Tests/EditMode/Gameplay/SwordSwingAudioTests.cs: pruebas adaptadas al audio confirmado por daño.

Nuevos:

- Assets/Scripts/Gameplay/Editor/RideFeatureSetup.cs y su .meta: migraciones enfocadas mediante SerializedObject/Undo/SaveScene; no regeneran la escena.
- Assets/Tests/EditMode/Gameplay/RequestedRideFeaturesTests.cs y su .meta: cuatro combinaciones de mano y dos sentidos de dirección.
- Tools/Verification/InspectRequestedFeatures.cs: inspección de arquitectura/escena.
- Tools/Verification/VerifyRidePriorities.cs: comprobaciones deterministas, Play Mode, capturas y ejecución directa de aserciones enfocadas.
- Este informe y evidencias en Docs/Verification.

## 4–6. Prefabs, objetos y escena

Ningún archivo prefab se modificó. JapanDemonHunter.unity se guardó mediante API del Editor; no se editó YAML ni se reconstruyó la escena.

Cambios localizados:

- Ambos ReinHandle: requireExpectedHand = false, enableLaneGesture = true.
- Knife: posición y deckLocalHeight; referencia y límites de recuperación; audio de agarre y cuatro impactos. Conserva Rigidbody, HandGrabInteractable, colliders, escala, daño y cinco zonas de agarre.
- VehicleRoot: nuevo hijo SwordRespawnPoint; configuración de audio y daño de HandStrikeController; volumen de muerte en GameSoundscape.
- MonsterSystem: GameSessionController existente añadido como componente serializado, para exponer parámetros en Inspector.
- SwordDamage de la espada: nuevo cooldown serializado en cero.
- RideIntroMenu y ResultadoEnCarreta se crean solo en runtime por el controlador de sesión existente.

Los cambios preexistentes en Meta Quest.asset, QuestApkBuilder, golpe.mp4 y otros archivos ajenos a esta implementación no se atribuyen a ella. Archivos de bindings/runtime del simulador pueden aparecer sin seguimiento al ejecutar el Editor; no se eliminaron.

## 7. Inspector

| Componente | Campos nuevos / configuración relevante |
|---|---|
| ReinHandle | requireExpectedHand=false; enableLaneGesture=true. Umbrales existentes: laneThreshold=0,18 m, rearmRadius=0,12 m, gestureCooldown=0,35 s. |
| CarriageMotor | Reutiliza laneWidth=2,8 m y laneShiftDuration=0,8 s; pausa mediante API, sin otro controlador de dirección. |
| GrabbableWeapon | swordRespawnPoint, swordSafeCenter=(0;1,460249;−0,305436), swordSafeHalfExtents=(1,589657;1,15;2,194564), swordRespawnDelay=1,25 s. |
| SwordSwingAudio | weapon, grabClip=5.wav, grabVolume=0,35; impactos 1–4.wav y volume=0,35. |
| HandStrikeController | handHitCooldown=0,35 s; handHitClip=golpe.mp3; handHitAudioSource; handHitVolume=0,5; punchDamage=8. Umbral existente minimumSwingSpeed=2 m/s. |
| SwordDamage | minimumWindowCooldown=0 para espada; 0,35 s en las ventanas de manos. |
| GameSoundscape | zombieDeathSoundVolume=0,512, limitado a [0,1]. |
| GameSessionController | waitForBothReins=true, presentationEye, introDistance=2,4 m, fallbackEyeHeight=1,6 m, introTitleSize=0,035, introInstructionSize=0,032, resultDistance=2,4 m. Campos antiguos preservados. |

## 8–9. Asociación y dirección

ReinHandle conserva expectedHand y la opción estricta para compatibilidad, pero la escena ya no exige correspondencia izquierda/derecha. Usa la mano seleccionadora del HandGrabInteractable existente, comprobando conexión y tracking. La gracia visual no cuenta como agarre. No se añadió otra aceleración ni se alteró WhipLashModel.

La dirección reutiliza BilateralReinGestureModel: promedio del desplazamiento local de ambos mangos respecto de su referencia adaptativa. El eje X y el umbral 0,18 m producen una orden izquierda/derecha; LaneTransitionModel interpola suavemente al carril adyacente en RoadPathModel. No se introdujo una dirección analógica continua porque ya existe un sistema funcional de tres carriles. Neutral mantiene el carril; soltar no recentra artificialmente la trayectoria. La entrada necesita volver a la zona de rearme para otra orden. El clip existente jalar latigo para girar.mp3 se reproduce por orden aceptada, nunca cada frame.

## 10–11. Recuperación y posición de espada

La zona segura es una caja en coordenadas de la carreta, derivada de los bounds de Wagon_Model. Mientras el SDK mantiene una selección de mano, la recuperación permanece cancelada, incluso si el tracking momentáneamente deja de ser válido. Fuera y suelta, el temporizador espera 1,25 s; al recuperar cancela velocidades lineal/angular y ventana de daño, vuelve al padre de transporte y al transform SwordRespawnPoint. Una espada suelta dentro continúa con su física anterior.

Nueva posición local: (0,55; 0,635249; −0,25), justo sobre el piso, a la derecha y alejada de ambas riendas. Rotación de reposo conservada. No se cambió alcance, escala ni hitbox.

## 12–13. Sonidos de espada

No existe un asset 5.mp3 en el inventario inspeccionado. El proyecto tiene el original 5.mp4 y su extracción importada 5.wav; se reutiliza esa extracción. Se reproduce una vez en la transición real no sostenida → sostenida, sin repetirse al sostener, oscilar o dañar.

Impactos: 1.wav, 2.wav, 3.wav y 4.wav, extraídos de los archivos originales existentes. Solo MonsterHit posterior a ApplyDamage exitoso en un enemigo terrestre puede dispararlos; se excluye explícitamente el sonido 5, se evita repetir el clip anterior si hay alternativas y se limita a uno por ventana existente. El feedback visual se conserva.

## 14–17. Combate de manos

Se reutilizan los HandGrabInteractor/IHand reales y los barridos SwordDamage existentes. PunchTip es una referencia invisible, no una mano visual ni un reemplazo XR. Movimiento relativo a la carreta evita golpes por el avance del vehículo. Solo una pose rastreada válida habilita el barrido.

Daño configurable: 8. Zombie actual: 30 HP; cuatro impactos de mano para morir, frente a 12 de la espada. Umbral 2 m/s. Ventana ya existente de 0,12 s y cooldown de rearme 0,35 s, más la colección de víctimas ya golpeadas por ventana, impiden daño continuo por permanecer en contacto. Aire y toque suave son silenciosos. golpe.mp3 se reproduce con PlayOneShot a 0,5 solo tras daño confirmado; ya no se reproduce indiscriminadamente por recibir daño de espada.

## 18–19. Muerte

zombie_muerte_Mahaha.mp3, duración importada 1,541 s. MonsterDamageable.Killed activa el emisor; guardia local evita repetición. Se conserva Kill(), estado Dead, liberación de enganche y retirada existentes. La demora de retirada del prefab supera la duración del clip.

Volumen anterior efectivo: AudioSource 0,32 × PlayOneShot 0,8 = 0,256. Nuevo: fuente 1 × deathSoundVolume 0,512 = 0,512, exactamente doble de ganancia nominal, dentro de rango. No equivale a asegurar ausencia de clipping de la mezcla total ni doble sonoridad percibida: requiere escucha en dispositivo. Murciélagos conservan su ganancia anterior.

## 20–22. Menú y Game Over

GameSessionController existente expone WaitingToStart, Playing y GameOver; OnGameStarted evita acoplamiento del panel con otros sistemas. Awake pausa motor, MonsterSpawner, GiantZombieSpawner, FaceBatThreatController y GameSoundscape antes de sus Starts. Update no avanza el tiempo durante espera. Dos IsHeld de los dos objetos, simultáneamente y sin restricción de lado, llaman StartGame una sola vez; se oculta el menú y se restauran los componentes que estaban habilitados. Se reutiliza la API del spawner para comenzar por agarre, sin exigir el primer latigazo. Soltar después no vuelve al menú.

Desktop Debug permite iniciar y acelerar con su control existente de latigazo, solo bajo UNITY_EDITOR. El código XR no contiene entradas de teclado añadidas.

Introducción y resultado son textos world-space con TextMesh existente, no dependencias nuevas. Se sitúan a 2,4 m delante de la dirección horizontal de la vista; se orientan una sola vez al aparecer. El Game Over sigue usando la lógica de derrota previa. La posición se calcula desde CenterEyeAnchor o la cámara desktop activa; no se mueve cámara ni rig. El título inicial se redujo después de detectar recorte en la primera captura.

## 23–25. Pruebas y errores

Evidencias:

- RidePriorities-2026-09-30.json: prioridades 1–7, todas PASS.
- MenuPlay-2026-09-30.json y GameOverPlay-2026-09-30.json: prioridades 8 y 9, PASS en Play Mode.
- FocusedAssertions-2026-09-30.json: aserciones NUnit invocadas directamente por reflexión en EditMode; no es una corrida del Unity Test Runner completo.
- DesktopControls-2026-09-30.json: mouse 360°, WASD, HUD, rig intacto, velocidad inicial 3,5, latigazo ~4,984 después del pequeño tiempo de desaceleración, coasting descendente, segundo latigazo, spawns Z/X/C/grupo y FSM ChaseAttachment.
- QuestDesktopRestoration-2026-09-30.txt: retorno de cámara XR, limpieza de cámara temporal, transform del rig intacto y OpenXR guardado sin cambios.
- Intro-2026-09-30.png y GameOver-2026-09-30.png: capturas de geometría/presentación desde cámara desktop. Revisadas; no certifican iluminación final ni legibilidad binocular en Quest.

Cada prioridad se compiló y se comprobó antes de avanzar. Fallos encontrados y corregidos: inicialización de pose de mano; referencia de tipo OVR no disponible en el ensamblado de Gameplay (resuelta sin añadir dependencia, usando transform serializado); título inicial demasiado ancho. Los harnesses requirieron sincronizar colliders, suscribirse explícitamente al crear objetos inactivos y capturar cursor para medir mouse. Intentar pruebas desktop con la escena temporalmente sucia fue rechazado por su guardia; se recargó la escena guardada después de retirar los objetos de prueba. Los diagnósticos CS1701 pertenecen al compilador efímero de verificación/Newtonsoft, no a código de gameplay.

No se repitió el runner asíncrono previamente bloqueado. El Editor tuvo cierres durante esta sesión; se reabrió el proyecto y se usó el pipeline ya instalado. La evidencia histórica 120/120 no se presenta como una corrida de estos cambios.

## 26–29. Límites y confirmaciones

Sin Quest 2 no se verificaron agarres físicos reales intercambiados, precisión de umbrales con hand tracking, ergonomía de alcance, comodidad binocular, mezcla de audio ni rendimiento Android. Las manos de prueba implementan IHand para comprobar el código del SDK y daño, pero no sustituyen esa prueba física. No se afirma una prueba funcional de Meta XR Simulator ni de headset en esta ronda.

Confirmado por código y medición: NO se modificó la cantidad de aceleración del latigazo (1,5 m/s en la escena). Máximo 8 m/s y desaceleración existente preservados. CarriageMotor solo recibió la pausa requerida por el menú.

Desktop Debug continúa funcionando: regresión de controles y restauración XR PASS. La escena queda guardada con desktopDebugMode=0; con ese valor el comportamiento normal sigue usando OVRCameraRig, Interaction SDK y OpenXR existentes. Configuración HandsOnly preservada. Esto confirma integración/configuración, no tracking real del visor.

## Entorno y reproducción

Unity 6000.6.2f1 se usa desde C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe. API local del Editor en Library/Pipeline/.unity-pipeline-port, paquete com.unity.pipeline existente. No se depende del comando unity en PATH.

AGENTS.md líneas 206–210 dice «De este proyecto (úsalos siempre que apliquen)» y enumera jdh-dev-loop, jdh-reins-gestures, jdh-monsters y jdh-xr-hand-interactions. No se encontraron en repositorio ni ubicaciones de agentes revisadas; no hay rutas concretas ni prohibición expresa del flujo equivalente. Se siguió la autorización del usuario para inspección directa, Editor vivo, compilación y verificaciones equivalentes. AGENTS.md no se modificó.

Abrir JapanDemonHunter, detener Play y usar Tools/Game/Ride Features para migraciones enfocadas. La escena ya está configurada; no ejecutar rebuild. VerifyRidePriorities.Priority1…Priority7 y FocusedEditModeAssertions requieren EditMode. Priority8Play requiere una nueva sesión Play esperando; Priority9Play y CapturePresentation requieren Desktop Debug temporal activo. La API run_script permite ejecutar cada entrada sin tocar assets de escena. Restaurar siempre el modo temporal y reabrir la escena guardada al terminar.

### Cierre de validación

La última compilación funcional (incluye prioridades 8/9, tamaño corregido del título y cambio privado PlayImpact) devolvió recompile_status completed, failed=false, compilationFailed=false. Después pasaron las 10 aserciones enfocadas, las comprobaciones deterministas 1–7 y la regresión desktop final. Los únicos cambios posteriores en scripts de gameplay fueron comentarios de documentación.

La ampliación opcional a más fixtures no llegó a ejecutarse: después del cierre del Editor, la reapertura registró Access token is unavailable, 0 entitlement groups y com.unity.editor.ui was not found en Logs/RideFeaturesFinalEditor.log. El pipeline no volvió a estar disponible; no se afirman resultados de esa ampliación. La evidencia FocusedAssertions contiene los 10 casos realmente ejecutados, no una suite completa. Se dejó el harness enfocado en esos fixtures comprobados.

Inspección final de archivos guardados: desktopDebugMode=0; maximumSpeed=8; startingSpeed=3,5; accelerationPerStroke=1,5; laneWidth=2,8; laneShiftDuration=0,8; handTrackingSupport=2. Sin diff en Assets/XR, Assets/Oculus ni Assets/Plugins/Android. git diff --check solo señala el espacio estándar que Unity serializa en m_Name del componente añadido; no se corrigió editando YAML con el Editor abierto.
