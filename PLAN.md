# WebDesktop — Plan de Desarrollo a Producción

> Framework interno · .NET 9 · Apps de negocio
> Prioridades: MVP funcional > Estabilidad > DX > Arquitectura

---

## Fase 1: Limpieza y Estabilización de la Base

- [x] 1.1 Migrar a .NET 9 (TargetFramework `net9.0-windows` en los 3 csproj, actualizar paquetes NuGet)
- [x] 1.2 Eliminar código muerto en TestApp (`Form1.cs`/`Form1.Designer.cs`/`Form1.resx` eliminados)
- [x] 1.3 Dispose correcto de recursos (`Dispose(bool)` en WebWindow limpia WebView2, ExternalInvoker y desuscribe eventos)
- [x] 1.4 Corregir evento `WebMessageReceived` (cambiado a `public event` + `protected virtual OnWebMessageReceived` — patrón .NET estándar)
- [x] 1.5 Eliminar código duplicado de Externo (3 bloques → 1 bloque limpio; bug `originalExternal` corregido)
- [x] 1.6 Configurar modos Debug/Release (`DebugType=portable`, `TreatWarningsAsErrors=true`)
- [x] 1.7 Corregir `.done()` → `.then()` en TestApp (jQuery Deferred vs Promise nativa)
- [x] 1.8 Corregir `ModalWindow` (constructor, evento nullable, supresión WFO1000)
- [x] 1.9 Build: 0 errores, 5/5 tests pasando ✅

## Fase 2: Refactor Arquitectónico

- [x] 2.1 Desacoplar `JavaScriptBridge` de `IJSRuntime` (eliminada dependencia Blazor; usa solo `IJSExecutor` + WebView2)
- [x] 2.2 Separar configuración de WebView2 (nueva clase `WebView2Configuration` con UserDataFolder, Language, AllowDevTools, etc.)
- [x] 2.3 Mejorar manejo de errores (nueva clase `WebDesktopException`, reemplazados `InvalidOperationException`)
- [x] 2.4 Hacer `ExternalInvoker` thread-safe (`ConcurrentDictionary` en vez de `Dictionary`)

## Fase 3: Features para Apps de Negocio

- [x] 3.1 Soporte offline para assets (CSS/JS globales con `InjectGlobalStyle`/`InjectGlobalScript`, sin CDN)
- [x] 3.2 API de diálogos nativos (`__dialog.showMessage`, `__dialog.openFile`, `__dialog.saveFile`, `__dialog.selectFolder`)
- [x] 3.3 Sistema de notificaciones (Windows Toast desde C#/JS)
- [x] 3.4 Eventos de ciclo de vida (`OnBridgeReady`, `OnNavigating`, `OnNavigated`, `FormClosingEvent`)
- [x] 3.5 Minimizar a bandeja (`EnableTrayIcon()` con NotifyIcon)

## Fase 4: Assets desde Archivos (DX)

- [x] 4.1 `SetAssetFolder(folder, virtualHost)` — mapea carpeta local a dominio virtual vía `SetVirtualHostNameToFolderMapping`
- [x] 4.2 `NavigateToAsset(htmlFile)` — carga HTML desde la carpeta de assets (ej: `index.html`)
- [x] 4.3 Los archivos HTML/CSS/JS se editan por separado con sintaxis completa (no más strings C#)
- [x] 4.4 La carpeta `wwwroot/` se copia automáticamente al output via `.csproj`
- [x] 4.5 Ejemplo funcional: gestor de tareas con `index.html` + `css/app.css` + `js/app.js`

## Fase 5: Calidad y Testing

- [ ] 5.1 Tests de integración (WebWindow real, ciclo completo de inicialización)
- [ ] 5.2 Tests de ExternalInvoker (handlers, casos borde, excepciones)
- [ ] 5.3 Tests de menús (agregar, remover, habilitar/deshabilitar)
- [ ] 5.4 Análisis de memoria (fugas en WebView2, ciclos C# ↔ JS)
- [ ] 5.5 Prueba de carga (ventanas modales anidadas, comunicación intensiva)

## Fase 6: DX y Documentación

- [ ] 6.1 Actualizar README (docs bilingües, ejemplos reales de app de negocio)
- [ ] 6.2 XML Docs en toda la API pública (`<summary>`, `<param>`, `<returns>`)
- [ ] 6.3 Versioning semántico (`AssemblyVersion` + `FileVersion` en csproj)

## Fase 7: Preparación para Producción

- [ ] 7.1 Firma de assemblies (strong name con `.snk`)
- [ ] 7.2 CI/CD básico (script de build Release + tests + empaquetado)
