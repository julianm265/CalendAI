# Proyecto CalendAI - Backend

Sistema de gestión de calendarios, equipos y eventos impulsado por asistencia de IA.

---

## Estado del Pipeline CI
![CI Pipeline](https://github.com/tu_usuario/tu_repositorio/actions/workflows/ci.yml/badge.svg)
*El estado actual de la integración continua se verifica en cada push y pull request mediante GitHub Actions.*

---

## Cómo levantar el proyecto desde cero

### Requisitos previos
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) instalado.

### Pasos de ejecución
1. **Clonar el repositorio:**
   ```bash
   git clone [https://github.com/tu_usuario/tu_repositorio.git](https://github.com/tu_usuario/tu_repositorio.git)
   cd tu_repositorio/CalendarioBackend

2. **Restaurar dependencias y compilar:**
   dotnet restore
   dotnet build

3. **Ejecutar la API:**
   dotnet run --project src/CalendarioBackend.Api

## Cómo correr las pruebas automatizadas
Las pruebas unitarias y de integración se ejecutan localmente con un solo comando:
**dotnet test**

**Nota: Asegúrate de ejecutar este comando estando dentro de la carpeta CalendarioBackend.**

### Nota sobre el uso de Inteligencia Artificial (IA)
**Herramientas utilizadas:** Asistentes de IA (Gemini / Copilot).

**Alcance de uso:** La IA se utilizó como apoyo para el diseño de pruebas unitarias (xUnit), configuración del flujo de GitHub Actions (ci.yml) y estructuración del AutenticacionService.

**Verificación:** Todo el código generado o sugerido fue revisado, probado y adaptado por el equipo para garantizar su correcto funcionamiento.
