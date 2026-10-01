# Demon Hunter: Last Ride — cierre técnico, 2026-10-01

Implementación y verificación mediante el Editor abierto, sin builds, commits ni reconstrucción de JapanDemonHunter. Evidencia nativa en `Docs/Verification/NewRound*.json`; los resultados están dentro de `details.result`. El campo exterior `passed` confirma el transporte del puente y no reemplaza las aserciones internas.

## Informe solicitado

1. **Prioridades 1–9:** 1 conservada, previamente validada con 45 pruebas/114 aserciones; 2–9 implementadas y verificadas en Editor. Las comprobaciones visuales, físicas y de rendimiento de Quest siguen pendientes.
2. **Scripts modificados:** `Gameplay/DesktopDebugMode.cs`, `Gameplay/Editor/RideFeatureSetup.cs`, `Gameplay/GameSessionController.cs`, `Gameplay/HandStrikeController.cs`, `Gameplay/LevelVictoryController.cs`, `Monsters/MonsterAnimationController.cs`, `Monsters/MonsterBase.cs`, `Monsters/MonsterSpawner.cs`. Cambios anteriores de prioridad 1 en `Reins/ReinDrivingModel.cs`, `Reins/ReinHandle.cs` y `Tests/EditMode/Gameplay/RequestedRideFeaturesTests.cs` conservados. Herramientas de verificación actualizadas: `VerifyDesktopControls.cs` y `VerifyRidePriorities.cs`.
3. **Scripts nuevos:** `Gameplay/CastleGoalTrigger.cs` y `Gameplay/Editor/RideLocalVerificationBridge.cs`. Herramientas: `Tools/Verification/{Invoke-RideLocal.ps1,SetupNextRound.cs,VerifyNextRound.cs,InspectNextRound.cs}`. Nuevos shaders URP: `Assets/Shaders/{CastleBeacon.shader,VictorySpark.shader}`.
4. **Prefabs modificados:** `Assets/Art/Monsters/Zombie/Prefabs/ZombieDemon.prefab` y `ZombieHordeDemon.prefab`. El segundo es el realmente conectado al spawner principal. Enemigos especiales preservados. `Assets/Art/Zombie/Zombie.fbx.meta` configura el loop del Run importado.
5. **GameObjects:** configuración localizada de spawner, sesión, controlador de victoria, manos y camino existentes. Hijos nuevos del reino: `Fortress_Model`, `CastleEntryFloor`, `CastleWarmLights` con dos luces, `CastleGoalTrigger`, `VictoryFireworks` con tres emisores. Decoración anterior del reino desactivada y conservada. Cámaras existentes conservadas; far clip aumentado a 2000 m.
6. **JapanDemonHunter.unity:** bindings del feedback, parámetros de encuentros, camino de 1440 m, reino alineado al final, castillo de escala fija, trigger y celebración. Guardada mediante APIs del Editor; no se editó YAML ni se regeneró la escena.
7. **Inspector:** `initialSpawnDelay=7`, `postEncounterSpawnDelay=15`, encuentros habilitados, probabilidad frontal 0,25, grupos laterales 2–4, distancia lateral 8–14 m, adelante 18–26 m, separación de troncos 1,2 m y 32 intentos de colocación. Feedback disponible a 0,85 m, glow preparado 0,65, pulso de golpe 0,18 s; referencias visuales por mano. `runState`, referencias de goal y partículas configurables. Balance expuesto en los componentes existentes.
8. **Bloqueo de dirección:** el cooldown compartido conservaba la orden anterior; el rearme también dependía del desplazamiento longitudinal. La corrección previa separa el rearme lateral y permite invertir ante un movimiento contrario válido.
9. **Steering corregido:** inversión atraviesa neutral sin conservar el bloqueo anterior; sostener una dirección no repite comandos. Se conserva el movimiento suavizado entre los tres carriles curvos existentes. No se reemplazó CarriageMotor.
10. **Sensibilidad final:** default de código `laneThreshold=0,15`; valores serializados actuales de ambos mangos **0,18**. Se respetó la instrucción de no volver a modificar prioridad 1. No se afirma haber migrado la escena a 0,15. Ruido, neutral e inversiones tienen pruebas deterministas.
11. **Vida normal:** 12 HP en ambos prefabs normales.
12. **Espada:** 12 de daño. Una ventana válida mata; detección de swing preservada.
13. **Manos:** 8 de daño. Primer golpe deja 4 HP; segundo mata. Velocidad mínima 2 m/s y cooldown 0,35 s conservados.
14. **Feedback:** reutiliza materiales de las manos Meta existentes mediante `MaterialPropertyBlockEditor`, `_FingerGlowColor` y propiedades de glow de los dedos. Bindings izquierdo/derecho para visuales OVR/OpenXR; excluye fantasmas de agarre a distancia. Cian cuando la mano rastreada está libre y cerca de un enemigo vulnerable; dorado breve tras daño válido. Restaura los valores originales al deshabilitarse o dejar de estar disponible. No crea manos ni convierte contacto suave en daño.
15. **Primer spawn:** 7 s desde habilitación por StartGame, nunca durante el menú. Medición nativa: **7,003 s**. La prueba inicial también observó murciélagos del sistema existente; las reglas de grupos descritas se aplican a zombis normales.
16. **Frontal/lateral:** nuevas direcciones laterales anexadas al enum existente; eventos relativos a la posición/orientación de la carreta. Se conserva el sistema separado de amenazas de murciélagos.
17. **Máximo frontal:** un zombi por evento. Seis eventos forzados verificados.
18. **Laterales:** grupos configurables de 2–4, sujetos a espacio disponible y máximo de activos; una colocación insegura puede reducir el grupo.
19. **Bosque:** offsets relativos al recorrido, raycast sobre `MonsterGroundSurface`, holgura física, distancia al jugador y separación de zombis. Además verifica raíces `Tree_...` del ForestRoad, porque los modelos de árboles inspeccionados no tenían colliders. No garantiza ausencia de toda intersección con copas visuales: pendiente inspección Quest.
20. **Run:** prefab principal usa `LocomotionFast` de `ZombieHorde.controller`, asociado al clip importado `Zombie|ZombieRun`. `MonsterAnimationController.PlayRun` usa el estado configurable en Chase/ChaseAttachment; fallback al locomotion existente si no está configurado. Velocidad de persecución 6,8 m/s conservada; loop declarado en importer. Se verificaron 42 estados/clips.
21. **Descanso:** miembros del encuentro registrados por instancia; Died/BecameInactive los retiran. Solo terminar el encuentro completo programa el siguiente a +15 s. Cadáveres posteriores no reinician el timer. Medición **15,013 s**; mientras quedan miembros vivos no se crea otro encuentro normal.
22. **Duración:** recorrido 1440 m, 80 tiles de 18 m. Simulación con motor real, spawner, ataques y trigger físico: **185,682 s (3 min 5,7 s)**; otra corrida dio 187,097 s. Editor acelerado a timeScale 12, latigazo cada 1,5 s y defensa automática 4 s después del spawn, 26 zombis normales y 124 latigazos. No hubo teletransporte ni timer de victoria. Es una simulación representativa, no una medición humana ni garantía de máximo absoluto ante inactividad.
23. **Modelo:** asset existente `Assets/Art/Fortress.fbx` (capitalización real). Posición world de Fortress_Model aproximadamente **(-248,83; -19,32; -1677,94)**. El offset vertical compensa el origen del mesh; entrada a altura del camino. Entrada orientada hacia el inicio y alineada con la trayectoria final.
24. **Escala:** fija **(320,320,320)**; no se anima. Se eligió tras inspeccionar el mesh y comprobar la visibilidad a aproximadamente 1,4 km entre árboles. Su tamaño monumental requiere valoración estética en Quest.
25. **Iluminación:** material `Mat_CastleBeacon`, shader URP sencillo con color cálido, emisión y sombreado fijo por normal; solo el castillo omite niebla para ser visible desde el inicio. Respeta profundidad. Dos luces doradas sin sombras, intensidad 3/rango 18. No se cambió la niebla global ni se añadieron decenas de luces. Partículas usan `Mat_VictorySpark` y shader aditivo compatible con estéreo.
26. **Entrada:** abertura aproximada a escala final **49,6 m de ancho ×34 m de alto**. Veinte rayos comprobaron un paso útil de **9,86 m ×4 m** para los tres carriles, caballos/carreta, sin colisión con el mesh. Suelo de entrada 18×32 m. Medidas geométricas de Editor, pendiente percepción física.
27. **CastleGoalTrigger:** BoxCollider trigger 11×6×3 y Rigidbody cinemático; filtra la CarriageMotor configurada y exige que el root real de la carreta esté dentro, evitando victoria prematura por caballos. Centro aproximado **(-199,78; 2,35; -1413,79)**, 8 m tras la referencia de entrada elegida. Evento único e idempotente.
28. **Victory:** amplía GameSessionController existente, conservando el valor serializado de GameOver. LevelVictoryController escucha el goal; con goal conectado no usa la antigua victoria por distancia. Detiene motor, spawns y amenazas, retira monstruos, evita daño nuevo y no activa derrota.
29. **Fuegos artificiales:** tres Particle Systems, máximo 64 partículas cada uno (192 total), bursts de 24, sin emisión continua por segundo, sin sombras; desactivados antes de ganar. Se observaron 48 partículas vivas en la prueba de victoria.
30. **CONGRATULATIONS:** presentación world-space existente, dorada, frente al jugador a **2,4 m**. Celebración visible; GameOver mantiene su presentación roja separada. Captura comprobada en `Verification/NewRound9-Victory.png`.
31. **Tests:** compilación del Editor tras cada etapa; balance Edit/Play sobre el prefab de escena, feedback Edit/Play, combate de ambas manos, timers, encuentros frontales/laterales, Run, duración, geometría/render y victoria física. Regresiones de espada, audio, muerte, menú, GameOver, Desktop y restauración XR. Puente local usado cuando HTTP no respondió.
32. **Resultados:** todas las verificaciones finales descritas pasaron. Tabla de evidencia abajo. Se invocaron 13 casos NUnit enfocados en el Editor; **no se ejecutó la suite completa de 120 con Unity Test Runner esta ronda**. La evidencia previa de 45 pruebas/114 aserciones de prioridad 1 se conserva, sin atribuirla a una nueva ejecución.
33. **Problemas encontrados:** el primer balance comprobaba ZombieDemon, pero la escena usa ZombieHordeDemon; se corrigieron ambos y se repitió Edit/Play con la referencia real. Pipeline HTTP inaccesible resuelto con puente local. Una interrupción/reinicio del Editor no provocó reintentos indefinidos. Se corrigió un literal de shader y se comprobó render sin errores. El castillo inicialmente quedaba oculto por bosque/niebla; se ajustó escala fija y material local. Warnings previos de APIs obsoletas no se ampliaron a refactors.
34. **Pendiente Quest 2:** calidad/sensibilidad de gestos reales, visibilidad y comodidad del glow, audio percibido, trayectoria/entrada y escala estética del castillo, duración humana, FPS/memoria y shaders en Android. No se ejecutó Meta XR Simulator esta ronda ni se afirma rendimiento de Quest.
35. **Latigazo:** confirmado **+1,5 m/s** por evento válido, sin cambiar cantidad. Máximo 8 m/s; desaceleración 0,2 m/s². La prueba de teclado observa también la desaceleración entre frames; la prueba nativa inmediata confirma la cantidad exacta.
36. **Desktop:** escena final guardada con **desktopDebugMode=0**. Prueba temporal de modo 1 pasó mouse, movimiento local, HUD, latigazo y spawns; V ahora solicita grupos laterales en la escena principal. Código exclusivo de escritorio permanece aislado en UNITY_EDITOR. Restauración de configuración y modo 0 verificada.
37. **XR:** OVRCameraRig, Meta SDK, Interaction SDK y tracking no fueron sustituidos ni desactivados. XR startup permanece activo; no hay cambios en Assets/XR, Assets/Oculus, ProjectSettings ni Packages. No se movió artificialmente cámara/XR Origin para gameplay. La configuración normal vuelve al modo XR; esto no equivale a una prueba física del headset.

## Evidencia final

Todos los nombres siguientes viven en `Docs/Verification/` y terminan en `-2026-10-01.json`.

| Verificación | Archivo / resultado |
|---|---|
| Balance real conectado | `NewRound2-sceneBalanceEdit`, `NewRound2-sceneBalancePlay`: PASS, 12/12/8, 1 espada/2 manos |
| Feedback | `NewRound3-feedbackEdit`, `NewRound3-feedbackPlay`: PASS, 20 comprobaciones cada uno |
| Manos | `NewRound3-handRegression`: PASS, izquierda/derecha, contacto suave, cooldown/audio |
| Primer spawn | `NewRound4-initialSpawnPlay`: PASS, 7,003 s |
| Encuentros y Run | `NewRound5-placementPlay`: PASS, frontal 1, lateral ambos lados, dispersión, 42 Run |
| Descanso | `NewRound6-cooldownPlay`: PASS, 15,013 s, sin reinicio por cadáver |
| Duración | `NewRound7-durationPlay`: PASS, 187,097 s |
| Castillo | `NewRound8-geometryEdit`, `NewRound8-startPlay`: PASS, paso libre, visible desde inicio |
| Victoria | `NewRound9-victoryJourneyPlay`: PASS, trigger físico, 185,682 s, celebración y enemigos detenidos |
| Modelos/gestos | `NewRound-regressionNUnit`: PASS, 13 casos enfocados |
| Espada | `NewRound-swordRecoveryRegression`, `NewRound-swordPositionRegression`, `NewRound-swordAudioRegression`: PASS |
| Muerte | `NewRound-deathAudioRegression`: PASS, una vez, volumen 0,512 (antes 0,256) |
| Sesión | `NewRound-finalRuntimeRegression`, `NewRound-gameOverPlay`: PASS, menú, +1,5, bloqueo de spawn/daño al ganar, derrota separada |
| Desktop/XR | `NewRound-desktopControls`, `NewRound-desktopXrRestoration`, `NewRound-desktopSettingsRestored`: PASS |
| Escena final | `NewRound-finalAudit`: PASS, detenido, modo 0, XR activo, parámetros/audio reales |

Espada conservada en local (0,55; 0,635; -0,25), escala/hitbox intactas. Recuperación: dentro permanece, fuera vuelve tras 1,25 s, sostenida no teletransporta. `5.wav` solamente al agarre; impactos 1–4 solo al registrar daño, sin repetición inmediata ni spam de contacto. Mano usa `Assets/Art/Audio/golpe.mp3`, volumen 0,5. Muerte usa `Assets/Art/Audio/zombie_muerte_Mahaha.mp3`, volumen configurable 0,512, una vez, con retirada de lógica activa.

## Repetir comprobaciones sin HTTP

Con JapanDemonHunter cargada y Play detenido, ejecutar en PowerShell desde la raíz:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
./Tools/Verification/Invoke-RideLocal.ps1 status -TimeoutSec 15
./Tools/Verification/Invoke-RideLocal.ps1 verification -File Tools/Verification/VerifyNextRound.cs -Entry VerifyNextRound.FinalAudit -TimeoutSec 20
```

El puente es solo Editor; usa solicitudes identificadas y caducadas y lista cerrada de herramientas. No necesita otra instancia de Unity, no genera builds. Las herramientas de setup se aplicaron mediante APIs; no ejecutar Rebuild Playable Scene para revisar estos cambios. Se preservaron assets de recuperación y el modelo Fortress del usuario. Las capturas usan cámaras temporales de diagnóstico, sin alterar el tracking del rig.

## Cierre adicional de prioridad 1 � sensibilidad aplicada
Tras la nueva autorizaci�n del usuario se complet� la migraci�n pendiente: ambos ReinHandle de JapanDemonHunter tienen ahora laneThreshold=0,15 m (antes 0,18). Esto sustituye el estado descrito en el punto 10 del informe. Se utiliz� SteeringSensitivityRound existente mediante API Editor y se guard� la escena, sin cambiar c�digo de movimiento ni aceleraci�n.
Validaci�n nueva: 45 casos de regresi�n nativos, 13 casos enfocados (incluidas inversiones y cuatro combinaciones mango/mano), Play Mode con neutral/ruido 0,05 m, desplazamiento subumbral 0,14 m, respuesta bilateral a 0,16 m, inversiones izquierda-derecha y derecha-izquierda, transici�n sin salto y sin comandos repetidos al sostener. Todos PASS. Auditor�a final confirma umbrales 0,15/0,15, latigazo +1,5, desaceleraci�n 0,2, m�xima 8, desktopDebugMode=0 y XR activo. Evidencias Priority1-*.json.
Dos intentos del harness se corrigieron: la prueba de umbral debe capturar primero la l�nea base neutral, y la medici�n de latigazo debe comenzar una sesi�n fresca, antes de que enemigos/derrota bloqueen el input. No se cambi� producci�n para satisfacer esas pruebas. Implementaci�n completa en el entorno disponible; aprobaci�n del tacto y sensibilidad con tracking real en Quest contin�a pendiente. Sin builds ni commits.

El runner standalone de las 114 aserciones anteriores no pudo ejecutarse por falta de netstandard 2.1 en su proceso; no se cuenta como PASS nuevo. Las 45 regresiones y 13 casos enfocados s� se ejecutaron nativamente en el Editor, adem�s del Play Mode descrito.
