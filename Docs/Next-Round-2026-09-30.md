# Nueva ronda de mejoras — estado del 30/09/2026

Esta ronda NO está terminada. La prioridad 1 tiene corrección de código y verificación determinista, pendiente de compilación del proyecto, migración de escena y Play Mode. Prioridades 2–9 pendientes, respetando la condición de no avanzar sin validar la anterior. No se hicieron builds ni commits.

## Prioridad 1: causa, corrección y sensibilidad

ReinGestureStateMachine exigía retornar a neutral en el plano X/Z y mantenía cooldown compartido antes de permitir otra dirección. Una rienda desplazada hacia atrás podía bloquear steering aunque X cruzara neutral; una inversión rápida también podía llegar durante el cooldown. No había un bloqueo en LaneTransitionModel: su Begin acepta interrumpir una transición y partir de CurrentX.

La corrección recuerda únicamente la última dirección lateral aceptada. El rearme lateral comprueba X; el freno heredado conserva su rearme plano. Una inversión clara que supere laneThreshold puede interrumpir exclusivamente la orden lateral anterior, incluso si el tracking salta la muestra de cero. Mantener la misma dirección no repite órdenes. Fire de freno/latigazo y pérdida de selección borran esa memoria. El cooldown de latigazo, las velocidades y CarriageMotor no se modificaron en esta ronda.

Sensibilidad propuesta: laneThreshold 0,18 → 0,15 m (16,7 % menos desplazamiento), configurable con el campo existente. El valor por defecto del componente está actualizado; la escena guardada conserva 0,18 hasta que se ejecute la migración enfocada desde el Editor. rearmRadius 0,12 m, gestureCooldown 0,35 s, laneShiftDuration 0,8 s y recenterSpeed 0,18 m/s siguen intactos. No se añadieron parámetros redundantes.

Migración preparada: Tools/Game/Ride Features/New Round 1 Steering Sensitivity. Solo actualiza valores iguales al anterior default y preserva ajustes manuales. No se ejecutó: no hay acceso al Editor cargado. No se editó YAML de escena.

## Archivos de esta ronda

Modificados:
- Assets/Scripts/Reins/ReinDrivingModel.cs: inversión y rearme lateral.
- Assets/Scripts/Reins/ReinHandle.cs: nuevo default de sensibilidad, con nombre serializado preservado.
- Assets/Scripts/Gameplay/Editor/RideFeatureSetup.cs: migración idempotente preparada.
- Assets/Tests/EditMode/Gameplay/RequestedRideFeaturesTests.cs: dos direcciones con inversión rápida y prueba de ruido/cooldown del latigazo.

Nuevo: Tools/Verification/VerifySteeringReversalStandalone.cs. Compila el modelo real contra UnityEngine.CoreModule y Oculus.Interaction existentes y ejecuta comprobaciones sin sustituir tipos matemáticos. Ejecutables temporales en Library/SteeringVerification, fuera de Assets.

Ningún prefab ni GameObject de escena modificado en esta ronda. JapanDemonHunter.unity conserva desktopDebugMode=0 y aceleración 1,5 m/s del cierre anterior. Configuración XR sin cambios.

## Pruebas ejecutadas

- Compilación C# del modelo modificado con Roslyn instalado en Unity y referencias reales: PASS. No equivale a compilar todo el proyecto en el Editor.
- Mismo test contra el modelo anterior recuperado con git show: FAIL esperado en «Reversal during cooldown with backward offset».
- Modelo corregido: PASS, 114 aserciones. Incluyen ruido, primera orden en ambos sentidos, cambio rápido tras neutral con offset Z, giro sostenido sin repetición, inversión saltando la muestra neutral, continuidad de la interpolación al interrumpirla, destino opuesto, latigazo y respeto de su cooldown.
- Nuevos casos NUnit: preparados, no ejecutados en Unity Test Runner todavía.
- Play Mode, migración de sensibilidad y regresión integrada XR/Desktop: pendientes.

Evidencia: Docs/Verification/SteeringReversalRound-2026-09-30.json.

## Bloqueo de entorno concreto

Existe Unity.exe, PID 3360, ventana «Unity Editor Software Terms»; no existe Library/Pipeline/.unity-pipeline-port. No se lanzó una segunda instancia ni se aceptaron términos en nombre del usuario.

Logs/RideFeaturesFinalEditor.log líneas 29–33 registran Access token is unavailable, cero entitlements y com.unity.editor.ui was not found. Hace falta que el usuario revise la ventana de términos y resuelva licencia/inicio de sesión si se solicita. Después: cargar JapanDemonHunter, recompilar por pipeline, aplicar la migración preparada y verificar Play Mode antes de continuar a daño, feedback, spawns y castillo.

El informe final de los 37 puntos se completará después de implementar y verificar las prioridades restantes. No se atribuyen vida, daño, tiempos, escala del castillo ni duración de partida nuevos a código que todavía no se implementó. La aceleración de esta ronda no fue modificada; Quest 2 físico sigue pendiente.

## Continuación: compilación de ensamblados y regresión

Se reutilizaron las respuestas reales del compilador de Library/Bee/artifacts/1300b0aE.dag, cambiando únicamente las salidas a Library/SteeringVerification. Compilaron sin errores Reins, JapaneseDemonHunter.Gameplay.Editor, JapaneseDemonHunter.Gameplay.EditModeTests y Reins.EditModeTests. No se sobrescribieron DLLs de Library/ScriptAssemblies ni se lanzó Build Pipeline. Advertencias existentes: APIs FindObjectsSortMode obsoletas y AppDomain.GetAssemblies en tooling previo; no se ampliaron cambios para corregirlas.

El ensamblado de tests existente se cargó con Mono instalado por Unity, y sus aserciones NUnit se invocaron directamente. ReinDrivingModelTests y CartHitPenaltyModelTests: 45 casos PASS, cero fallos. Se utilizó el Reins.dll recién compilado. Esta ejecución no utiliza Unity Test Runner ni equivale a Play Mode, pero comprueba regresión de rearme, gestos, carriles y penalización.

Nuevo archivo reproducible: Tools/Verification/VerifySteeringRegressionStandalone.cs. Evidencia: Docs/Verification/SteeringRegressionRound-2026-09-30.json. Se mantienen las 114 aserciones del harness de inversión.

El Editor sigue en Unity Editor Software Terms y sin descriptor de pipeline. La migración de escena, validación Play/XR/Desktop y prioridades 2–9 continúan pendientes. No se interpretó «Continúa» como autorización para aceptar términos legales ni como eliminación de la validación obligatoria por prioridad.

## Continuación tras confirmación del usuario

Se repitieron las compilaciones aisladas de cuatro ensamblados, los 45 casos de regresión (45 PASS, 0 FAIL) y las 114 aserciones (PASS). No se reimplementó el steering.

Se preparó la siguiente migración localizada en RideFeatureSetup.SmallZombieBalanceRound: normal ZombieDemon.maximumHealth=12, conservando espada=12 y manos=8. Un impacto de espada mata; primer golpe de mano deja 4 HP y el segundo mata. Enemigos especiales y daños globales se preservan. El valor queda en el Inspector de MonsterDamageable. La migración usa PrefabUtility.LoadPrefabContents/SaveAsPrefabAsset/UnloadPrefabContents; no cambia YAML. Todavía NO se ejecutó ni se modificó el prefab.

Nuevo harness Tools/Verification/VerifyNextRound.cs: SteeringPlay para medir inversión suave en CarriageMotor, sensibilidad 0,15, ruido 0,05 y ganancia 1,5; SmallZombieBalance para comprobar el prefab real, daño y rechazo de impactos posteriores a muerte. Ambos compilados, pendientes de ejecución en Editor.

La conexión al Editor sigue sin estar disponible desde las herramientas: falta Library/Pipeline/.unity-pipeline-port y no responde ningún puerto TCP localhost 7800–7899. El log de la última sesión humana muestra carga del proyecto seguida de cierre. Los intentos de relanzamiento reutilizando contexto de Unity Hub no obtuvieron entitlement. No se afirma que la ventana del usuario siga bloqueada; solo que no hay un pipeline accesible en este entorno. Se pidió activar Start/Restart en Edit > Project Settings > Pipeline > Editor y comunicar estado/puerto para continuar.

Estado real: prioridad 1 validada como código/modelos, pendiente de aplicar sensibilidad en escena y Play Mode; prioridad 2 preparada, pendiente de aplicar/testear en Editor; prioridades 3–9 sin implementar. desktopDebugMode guardado sigue en 0; aceleración guardada 1,5; no builds ni commits.

## Vía local de Editor para prioridad 2

Se añadió Assets/Scripts/Gameplay/Editor/RideLocalVerificationBridge.cs (ensamblado Editor, sin cambios de XR/runtime). Es una alternativa limitada al HTTP: solo procesa solicitudes locales explícitas, identificadas y con caducidad. Acciones permitidas: status, balanceEdit y balancePlay. No permite código arbitrario, builds, reconstrucción ni borrado de escenas. balanceEdit usa la migración PrefabUtility existente. balancePlay exige escena guardada y Play detenido, ejecuta la prueba y sale de Play.

La prueba nativa preparada usa el prefab real, SwordDamage.Sweep y ventanas reales: espada 12 mata, repetición en la ventana no daña otra vez, primer golpe de mano 8 deja 4 HP, segunda ventana mata. También comprueba un impacto de espada distinto de 5 y un sonido de muerte por víctima. Los objetos de prueba son temporales y se retiran al terminar.

Compilación del ensamblado Editor con el nuevo puente: PASS, solo advertencias previas. NO se ejecutó la prueba nativa: no hubo respuesta después de 100 segundos; el .meta del puente no apareció, no existe Library/RideLocalReply.json y el prefab aún muestra maximumHealth=30. HTTP 127.0.0.1:7801 tampoco responde desde las herramientas, pese al estado Ready informado por el usuario.

Se pidió Assets > Refresh y confirmar que el Project Path sea D:/IHC-2026B/demon-hunter-last-ride. La solicitud de modificación se canceló al finalizar la espera; queda únicamente un status de corta duración, sin mutaciones pendientes. No se hizo una segunda instancia ni se editaron assets YAML en esta continuación.

Estado: prioridad 2 preparada, NO aplicada ni validada en Play Mode; prioridades 3–9 pendientes. No se afirman 12 HP reales hasta que responda el Editor y se guarde el prefab. desktopDebugMode sigue en 0, daño de espada en 12 y manos en 8. Sin builds ni commits.

## Cierre posterior — 2026-10-01
El bloqueo descrito arriba es histórico y quedó resuelto mediante RideLocalVerificationBridge. Prioridades 2–9 implementadas y verificadas en Editor; prioridad 1 conservada. Informe final actualizado: [Next-Round-2026-10-01.md](Next-Round-2026-10-01.md). Evidencia final en Docs/Verification/NewRound*.json. Quest físico pendiente; sin builds ni commits.

