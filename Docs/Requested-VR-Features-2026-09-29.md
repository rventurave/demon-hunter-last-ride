# Features VR solicitadas — 29 de septiembre de 2026

Estado: cambios aplicados y comprobaciones programáticas en Play Mode aprobadas. **La validación visual con tracking operativo, agarres reales y escucha en Quest sigue pendiente. No se considera cerrada la tarea.** El Editor dejó de responder al intentar completar la sesión real del simulador y después dejó de estar disponible; se solicitó reinicio al humano conforme a AGENTS.md §7.

## 1. Scripts modificados

- `Assets/Scripts/Gameplay/Editor/PlayableSceneCreator.cs`: conserva los mismos ajustes en futuras creaciones. No se ejecutó reconstrucción de escena.
- `Assets/Scripts/Reins/CarriageMotor.cs`: nombre de Inspector `decelerationRate`, con `FormerlySerializedAs("coastingDeceleration")`, y protección contra valores negativos. Conserva `MoveTowards(..., rate * deltaTime)`.
- `Assets/Scripts/Monsters/MonsterSpawner.cs`: opción adicional `FrontDispersed`; reutiliza validación de terreno, holgura y radios. No incorpora estos zombis a la lista de la horda trasera. Conserva `FrontLane` para murciélagos.
- `Assets/Scripts/Monsters/CartAttachmentPoints.cs`: opción para permitir reservas laterales, manteniendo la restricción trasera por defecto para otras escenas.
- `Assets/Scripts/Monsters/AttachedMonsterAttack.cs`: reproduce el ataque existente fuera del alcance del jugador como feedback visual, sin aplicar daño fuera del rango ni cambiar su cooldown.
- `Assets/Scripts/Monsters/SwordDamage.cs`: mide velocidad en espacio local de la referencia móvil para que un giro de la carreta no sea interpretado como un swing.

## 2. Scripts nuevos

- `Assets/Scripts/Gameplay/Editor/RequestedFeaturesSetup.cs`: menú que actualiza la escena abierta en el orden 1–7, sin reconstruirla. Reutiliza objetos/componentes y conserva ajustes personalizados de luz, arma y audio cuando ya están migrados.
- `Assets/Scripts/Gameplay/SwordSwingAudio.cs`: feedback aleatorio vinculado a `SwordDamage.AttackWindowOpened` y reacción visual en `MonsterHit`.
- `Assets/Scripts/Gameplay/MonsterSwordHitFeedback.cs`: tintado breve de los meshes existentes mediante `MaterialPropertyBlock`, restaurando sus bloques originales.
- `Assets/Tests/EditMode/Gameplay/SwordSwingAudioTests.cs`: cuatro pruebas enfocadas.
- `Tools/Verification/VerifyRequestedPlay.cs`: comprobación programática ejecutable mediante `unity command run_script` desde Play Mode; vive fuera de Assets y no se incorpora al jugador.

## 3. Escena y GameObjects modificados

Escena: `Assets/Scenes/JapanDemonHunter.unity`, guardada mediante el Editor. No se editó su YAML.

- `OVRCameraRig`: y inicial local 0,695 → aproximadamente 0,560. El piso se obtuvo del modelo actual; un raycast contra una copia temporal de su malla dio y=0,5581. Mantiene orientación hacia −Z, donde están los caballos. No hay un `XR Origin` de Unity ni `CharacterController`; el equivalente al Camera Offset es `TrackingSpace`, que permanece a cero. OVR conserva `FloorLevel`; `FirstPersonLocomotor` permanece desactivado. No se añadieron offsets ni recentrados de cámara en runtime.
- `Knife`: reposo local `(0,450; 1,310; −0,950)`, aproximadamente 0,45 m hacia la derecha y 0,35 m delante del origen inicial, a 0,75 m sobre el piso. Usa el reposo transportado existente de `GrabbableWeapon`; no se añadió una mecánica de soporte.
- `Knife_Model`, `BladeTip` y `CapsuleCollider`: longitud 0,85 → 0,98 m; tip z=0,69; collider de longitud 0,98 centrado en z=0,20. Se conservaron componentes SDK de agarre y sus attach poses.
- `Knife`: un `AudioSource` y un `SwordSwingAudio`, con los cinco clips asignados. No se instancian fuentes por ataque.
- `MonsterSystem`: entrada terrestre frontal dispersa; horda trasera mínima desactivada en esta escena. Mantiene el tiempo de aparición existente.
- Enganches terrestres: se reutilizaron y renombraron tres puntos traseros y se añadieron dos laterales. `LeftAttachPoint`, `RightAttachPoint`, `RearLeftAttachPoint`, `RearRightAttachPoint`, `RearCenterAttachPoint`, todos con capacidad 1. Las coordenadas se derivan de los bounds del wagon; y local de las raíces=0,15. Se preservaron los puntos voladores existentes.
- `VehicleRoot/CarriageLampAmbience`: intensidad frontal 6→7 y trasera 5→6, conservando las cuatro luces, sus rangos y la ausencia de sombras en luces de carreta. `Moonlight` 0,34→0,40; ambiente `(0,006; 0,007; 0,008)`→`(0,012; 0,014; 0,016)`. No se modificaron URP ni exposición para esta feature.

## 4. Parámetros del Inspector

- `CarriageMotor.decelerationRate` (m/s²): conserva 0,20 en la escena. Cero desactiva coasting; no eleva la velocidad máxima.
- `MonsterSpawner.frontSpawnHalfAngle`: 12°. `MonsterSpawnEntry.spawnDirection` admite `FrontDispersed`; reutiliza radios y multiplicador de velocidad existentes.
- `CartAttachmentPoints.rearPointsOnly`: falso en la escena principal; verdadero por defecto para compatibilidad.
- `AttachedMonsterAttack.animateOutsideAttackRange`: verdadero; solo extiende feedback visual. Conserva rango, impacto, daño y cooldown.
- `SwordSwingAudio`: `swordDamage`, `audioSource`, `swordSounds[5]`, `volume`=0,35, `avoidImmediateRepeat`=verdadero.
- `MonsterSwordHitFeedback`: `flashDuration`=0,12 s, `hitColor`, `tintAmount`=0,45. Disponible en el componente creado al primer impacto.
- Transform de cada punto y su capacidad siguen editables. Intensidades, rangos y collider/tip usan sus campos existentes.

## 5. Clips encontrados

Originales: `Assets/Art/Audio/swordSound/1.mp4`, `2.mp4`, `3.mp4`, `4.mp4`, `5.mp4`. Los cinco contienen audio AAC. Se extrajo el mismo audio a `1.wav`–`5.wav` dentro de esa carpeta, mono a 44,1 kHz, sin crear contenido sonoro ni reemplazar los originales. Unity importó las cinco copias como `AudioClip` con nombres `1`, `2`, `3`, `4`, `5`.

Duraciones importadas: 0,994; 1,347; 1,144; 1,682; 1,422 segundos.

## 6–8. Aleatoriedad, repetición y activación

El componente cuenta clips válidos y elige uno mediante `Random.Range`. Ignora entradas nulas. Excluye el último `AudioClip` reproducido si queda alguna alternativa; con un único clip válido permite repetirlo. Una colección vacía o nula no reproduce sonido ni lanza excepciones.

El sonido se emite únicamente al abrirse la ventana de ataque existente, usando `PlayOneShot(clip, volume)`. Los contactos repetidos, `MonsterHit` y los barridos no vuelven a activar el sonido. `SwordDamage` conserva su registro por ventana para impedir daño repetido al mismo enemigo. El sonido de impacto separado de `GameSoundscape` se mantiene.

## 9–10. Pruebas realizadas y resultados

- Recompilación mediante Unity Pipeline completada, sin errores de compilación. Se conservaron advertencias heredadas de APIs obsoletas de Unity 6.
- EditMode enfocado, asíncrono: **4/4 aprobados**, en 0,12 s. Incluye 50 ventanas consecutivas sin repetir el clip anterior, 10 begins/sweeps dentro de una misma ventana, traslado/rotación de referencia sin falso ataque y clips nulos/parcialmente asignados.
- Play Mode, comprobación programática: **aprobada**. Se usaron la configuración de escena, los clips importados y el prefab real. Para los casos de persecución y traslado tras un adelantamiento se creó una carreta temporal **sin cámara ni rig XR**, con copias de los cinco puntos y terreno temporal. Se retiraron sus objetos al finalizar.
- Desaceleración: motor temporal de prueba a 20 m/s y tasa 1 m/s²: `19,980 → 18,898 → 17,860 → 16,794`, en 3,186 s. Pérdida medida=3,185953; diferencia con `rate × tiempo` menor de 0,00001 m/s. No se configuró la carreta real a 20 m/s.
- Spawn: 50 posiciones válidas delante; 28 a la izquierda y 22 a la derecha. Radios frontales observados aproximadamente 13–22 m, con x entre −3,605 y +4,031.
- Persecución después del adelantamiento de la carreta temporal: estado `ChaseAttachment`, giro hacia el destino móvil y desplazamiento de 3,40 m; no quedó detenido.
- Enganches: cinco ocupantes en cinco puntos, sexto zombi sin reserva; parenting correcto, velocidad de persecución cero, animación de ataque disponible y ciclo visual iniciado. Conservan pose relativa al trasladar/girar la carreta temporal.
- Alcance e iluminación: valores de collider/tip correctos; cuatro luces de carreta, sin nuevas sombras. Estas comprobaciones verifican configuración, no rendimiento ni calidad visual en Quest.
- **10 ataques consecutivos por apertura programática de la ventana existente**: secuencia `5, 2, 4, 5, 2, 4, 2, 1, 3, 5`. Se observaron los cinco sonidos, sin repetición inmediata y con una reproducción por ataque, incluso tras diez barridos/begins repetidos en cada ventana. En reposo no se añadieron emisiones. Colecciones parcial/nula/vacía seguras; un solo `AudioSource`.
- Impactos contra un prefab real: 30 de vida inicial, 12 de daño vigente por ventana, sin daño repetido dentro de la ventana; reacción visual y estado `Dead` al llegar a cero. Este resultado no cambia el balance.
- Volumen digital: gain del one-shot=0,35 (aproximadamente −9,1 dB). Picos originales WAV entre −1,9 y 0 dBFS; medias entre −22,4 y −12,8 dBFS. No se normalizaron ni reemplazaron los clips. **No se validó comodidad auditiva con un headset.**
- `git diff --check` para scripts/tests sin errores. La escena serializada por Unity contiene espacios finales de sus campos vacíos `m_Name`, que no se editaron manualmente.

Evidencia preservada:

- `Docs/Verification/RequestedFeaturesPlayMode-2026-09-29.json`.
- `Docs/Verification/SwordAudioEditMode-2026-09-29.json`.
- Diagnóstico/capturas locales adicionales en `Logs/`, carpeta ignorada por Git.

## 11. Problemas y verificaciones pendientes

- La primera comprobación detectó que puntos traseros exteriores quedaban fuera del alcance del jugador. Se corrigió únicamente la reproducción visual de mordida fuera de rango, manteniendo las condiciones de daño, y se repitió la comprobación con éxito.
- El SDK instalado usa el menú `Meta > Meta XR Simulator > Activate`, distinto de la ruta antigua documentada en AGENTS.md. El intento con la ruta antigua produjo un error de menú; no era un error del juego.
- En la primera corrida programática el simulador estaba activado, pero OpenXR no tenía una sesión de tracking operativa: `XR_ERROR_API_LAYER_NOT_PRESENT`, sin dispositivos y `CenterEyeAnchor` en cero. La captura muestra una vista baja y **no valida la altura real del jugador**.
- Para diagnosticar, se indicó temporalmente al proceso del Editor dónde encontrar la capa ya instalada de Meta XR Operator. No se cambió ninguna feature OpenXR ni el manifiesto. El siguiente intento detectó HMD, pero el simulador informó Quest 3 pese a la configuración solicitada para Quest 2 y después bloqueó el pipeline. No se repitieron corridas largas sobre el Editor bloqueado.
- Hace falta reiniciar el Editor, seleccionar Quest 2 en la UI del simulador y retomar: pose de cabeza rastreada y altura inicial, alcance y agarre real de la espada, diez swings activados por movimiento, comprobación visual de zombis/iluminación y escucha de volumen. Las diez ventanas programáticas aprobadas **no sustituyen swings rastreados ni agarres reales**.
- No se ejecutó la suite EditMode completa ni la validación global del generador en esta ronda; solo los cuatro tests enfocados y el runner funcional indicado. No se trasladan resultados antiguos de 120/120 a estos cambios.
- Unity/SDK generó `RuntimeActionBindings.json` y su copia de `Assets/StreamingAssets` al intentar iniciar OpenXR. Se dejaron para revisión, junto con los cambios previos del usuario en configuración, sin borrarlos ni revertirlos.
- Sin builds, commits, validación de Quest real ni mediciones de rendimiento en Quest.

## 12. Aceleración por latigazo

**No se modificaron fuerza, detección, cooldown ni lógica de incremento por latigazo.** El diff de `CarriageMotor` se limita a import de serialización, nombre/protección del campo de desaceleración y su lectura. En escena se verificaron los valores existentes: incremento 1,5 m/s y máximo 8 m/s. La cifra 20 m/s del ejemplo solo se utilizó en un motor temporal de prueba.

## Cómo aplicar y retomar

La escena ya fue actualizada. Si es necesario reaplicar sobre una copia o después de revertir, abrir `JapanDemonHunter` fuera de Play y ejecutar **Tools > Game > Apply Requested VR Features (in place)**. No ejecutar Rebuild/overwrite.

Pruebas enfocadas desde Unity CLI:

```powershell
unity command run_tests --mode EditMode --filter SwordSwingAudioTests --async_tests true
unity command test_status
```

Comprobación programática desde Play Mode, cuando el Editor vuelva a responder:

```powershell
unity command run_script --file Tools/Verification/VerifyRequestedPlay.cs --entry VerifyRequestedPlay.Run --timeout_ms 45000
```

El runner usa ventanas de ataque programáticas y una carreta temporal sin rig para ensayar adelantamientos/enganches. La revisión con tracking se realiza en la escena real, con Meta XR Simulator en Quest 2 y luego en el headset; el humano realiza cualquier build.
