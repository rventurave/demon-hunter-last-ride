# Quest 2: auditoría y preparación

## Estado y alcance

Se revisó la escena principal en Unity 6000.6.2f1 mediante el Editor vivo y Unity CLI 1.0.0-beta.11. No se generaron builds, no se actualizó Unity ni paquetes, no se hicieron commits ni se reconstruyeron escenas. Se conservaron los cambios previos del equipo.

**El proyecto real usa Meta XR Core / Meta Interaction SDK 205, OVRCameraRig y HandsOnly. No usa XR Interaction Toolkit, XROrigin ni InputActionManager.** No se instalaron sistemas alternativos ni se sustituyó el rig. La presencia de anclajes/controladores Meta y del perfil Oculus Touch no habilita por sí sola interacción de juego con mandos físicos. Esa interacción sigue diseñada para manos; la solicitud de mandos Touch necesita aclaración antes de cambiar ese contrato. No se afirma que el objetivo XRI/Touch esté completado.

## Cambios de esta revisión

- `Assets/XR/Settings/OpenXR Package Settings.asset`: se habilitó **Meta Quest Support** para Android mediante la API del Editor; se conservó Meta XR Feature y los perfiles existentes.
- `Assets/Scripts/Gameplay/Editor/QuestDiagnostics.cs` y `.meta`: ventana de diagnóstico de solo lectura en **Tools > VR > Quest Diagnostics**. Incluye paquetes, loaders e inicio XR por plataforma, features, validación oficial OpenXR, rig, cámaras, anclajes y componentes de controladores, InputActionManager, mapas de acciones cargados, DesktopDebugMode, arquitectura, backend, APIs, aplicación y soporte Android. En Play también consulta subsistemas y dispositivos XR de cabeza/manos. No confunde tracking configurado con tracking físico verificado.
- `Assets/Scripts/Gameplay/Editor/JapaneseDemonHunter.Gameplay.Editor.asmdef`: referencias a los ensamblados instalados de XR Management y OpenXR para el diagnóstico.
- `Tools/Verification/ConfigureQuestOpenXR.cs`: ajuste idempotente de Meta Quest Support mediante Editor vivo; no cambia escenas ni lógica de juego.
- `Tools/Verification/VerifyQuestSetup.cs`: informe reproducible de configuración, sin reemplazar una escena con cambios sin guardar.
- `Tools/Verification/VerifyQuestDesktopTransition.cs`: prueba de restauración Desktop preparada, **pendiente de ejecución** por desconexión/bloqueo del pipeline. No se guarda el checkbox de prueba. Si su petición pendiente llega a ejecutarse después del timeout, detener Play y restaurar el checkbox original antes de guardar.
- Este informe y `Docs/Verification/QuestSetup-2026-09-30.txt`.

No se modificaron DesktopDebugMode, DesktopDebugSetup, cámara, rig, gameplay, manifiesto Android ni versiones de paquetes en esta revisión.

## Verificación completada

| Comprobación | Resultado observado |
| --- | --- |
| OpenXR | 1.18.0 instalado; loader activo Android y Standalone |
| XR Plug-in Management | 4.7.0 instalado; Initialize XR on Startup activo en ambas plataformas |
| Features | Meta XR Feature y Oculus Touch Controller Profile activos en ambas plataformas; Meta Quest Support activo Android tras el ajuste |
| Render XR | Single Pass Instanced / Multi-view |
| Rig | Un OVRCameraRig activo bajo VehicleRoot; sin XROrigin XRI |
| Cámara | CenterEyeAnchor activa, MainCamera, stereo Both, near 0,1 m; cámaras auxiliares de ojos desactivadas, coherente con cámara central |
| Cabeza | OVRManager usePositionTracking y useRotationTracking true; tracking origin Floor Level; controllerDrivenHandPosesType None (0) |
| Izquierda/derecha | Dos anclajes por lado y un componente de controlador Meta por lado; no constituye prueba de hardware ni interacción Touch |
| Input | Input System 1.20.0; asset global con 2/2 mapas activos en Editor. Meta SDK usa su propio input. Sin InputActionManager XRI |
| Desktop | Checkbox false en escena observada/guardada; implementación exclusiva del Editor y sin cambios en esta ronda |
| Android | ARM64, IL2CPP, min API 32, target 34; Vulkan y OpenGLES3; Android Build Support instalado |
| Escenas | JapanDemonHunter habilitada en lista global y en override del perfil Meta Quest inspeccionado en disco |
| Manifest | Quest 2 incluido; hand tracking requerido y permiso HAND_TRACKING; HandsOnly (2) conservado |
| Compilación C# | Último recompile_status: completed, failed=false, errors=[] |
| Diagnóstico final completado | 0 errores y 10 avisos; detalle en el informe TXT |

La plataforma activa del Editor durante el diagnóstico fue **StandaloneWindows64**, no Android. El perfil Meta Quest existe pero hay que activarlo para Build And Run Android.

Avisos observados: XRI ausente, XROrigin XRI ausente, InputActionManager ausente (arquitectura Meta existente); Application Identifier Android del template; SSAO con coste significativo en dispositivo; recomendación opcional Prioritize Input Polling; cuatro avisos de features que solicitan una revisión de API OpenXR inferior a 1.1.54 (Android/Standalone). No se cambiaron paquetes por estos avisos ni ajustes visuales sin evidencia de hardware.

## Desktop Debug y límites de integración

La revisión de código confirma que DesktopDebugSetup clona XRGeneralSettings en memoria para omitir el inicio XR durante Desktop Debug y restaura la instancia original al salir de Play. No guarda loaders ni features. DesktopDebugMode crea una cámara independiente, suspende cámaras/listeners y componentes de agarre necesarios, y restaura los estados originales y la pose del arma al desactivarse. No destruye componentes XR ni mueve el rig. Sus controles y objetos temporales están bajo UNITY_EDITOR.

**Cambiar de true a false durante Play no inicia XR automáticamente.** Para XR, desactivar antes de entrar a Play y reiniciar la sesión. Es el contrato ya documentado del harness; no se reescribió. La prueba nueva de restauración no terminó: run_script agotó 30 s y editor_status también agotó 30 s; se detuvieron reintentos. No se ejecutó una nueva suite EditMode ni prueba en Meta XR Simulator o visor. Los resultados históricos de escritorio no se presentan como nuevos resultados.

## Ejecución y revisión manual

1. Reiniciar/recargar el Editor si el pipeline sigue bloqueado. Abrir `Assets/Scenes/JapanDemonHunter.unity` y **Tools > VR > Quest Diagnostics**, pulsar Actualizar diagnóstico.
2. Para escritorio: activar DesktopDebugMode antes de Play. Mouse/WASD, Space/Shift, Z/X/C/V y click siguen en el harness existente. Para volver a XR: detener Play, desactivar el checkbox y guardar la escena.
3. Android: activar **File > Build Profiles > Meta Quest** (Android), revisar la escena principal habilitada y ARM64/IL2CPP. El Application Identifier Android aún es `com.UnityTechnologies.com.unity.template.urpblank`: cambiar a `com.unsa.ihc.japandemonhunter` en los Player Settings del perfil que realmente se compilará y comprobar que persiste al guardar. El perfil contiene override de Player Settings; revisar tanto perfil como globales.
4. Unity Hub: comprobar Android Build Support, SDK/NDK/OpenJDK y External Tools. El diagnóstico detectó soporte de plataforma, pero no ejecutó Gradle para probar el toolchain completo.
5. Cuando Windows/ADB reconozcan el visor y esté autorizado para depuración, seleccionar **Run Device: Meta Quest 2** y ejecutar **Build And Run** manualmente. La configuración del proyecto no resuelve «USB device not recognized»; no se hicieron cambios de drivers, USB ni hardware.
6. Quest Link: en Meta Horizon Link seleccionar el runtime OpenXR de Meta como activo; establecer la conexión Link desde el visor. Desactivar Meta XR Simulator si estaba activo, reiniciar Unity si se cambió runtime y usar Play con DesktopDebugMode=false. [Guía oficial de Meta sobre Link](https://developers.meta.com/vr/documentation/unity/unity-link/). Verificar soporte de las features de manos sobre Link: no se garantiza equivalencia completa con Android.
7. Validar en Quest 2 tracking de cabeza/manos, agarres, gestos, interacción, confort y rendimiento. Revisar SSAO y los avisos OpenXR. No se certificó experiencia física ni rendimiento.

Reproducción de auditoría sin build:

```powershell
unity command recompile
unity command recompile_status
unity command run_script --file Tools/Verification/VerifyQuestSetup.cs --entry VerifyQuestSetup.Run --format json
```

La CLI usada en esta sesión se descargó a la carpeta temporal y se verificó por SHA-256 porque `unity` no estaba en PATH. No se cambió el PATH ni se instaló otra versión del Editor.
