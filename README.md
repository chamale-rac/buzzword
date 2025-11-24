# Buzzword

Juego de adivinanzas de ritmo rápido donde descifras frases generadas por IA para encontrar la palabra objetivo.

## Game Loop

1. **Generación de pistas**: Cada ronda, Gemini AI genera una frase descriptiva basada en la dificultad actual y el idioma seleccionado
2. **Adivinanza**: El jugador tiene tiempo limitado para escribir la palabra que mejor coincide con la descripción
3. **Evaluación**: El sistema valida la respuesta comparándola con la palabra objetivo y sinónimos aceptados
4. **Puntuación**: Se otorgan puntos base más bonificación por velocidad de respuesta
5. **Progresión**: 
   - Sistema de vidas (10 iniciales): +1 por acierto, -3 por fallo
   - Dificultad dinámica que incrementa cada 4 rondas
   - Tiempo disponible disminuye progresivamente
6. **Fin del juego**: Se acaban las vidas, con opción de reintentar o regresar al menú

### Características adicionales
- **Sistema de pistas**: Hasta 2 pistas por ronda (longitud de palabra y letra inicial)
- **Soporte bilingüe**: Inglés y español con contenido localizado

## Integraciones

### Gemini API (Google)
- **Modelo**: `gemini-2.0-flash-exp`
- **Función**: Genera frases descriptivas dinámicas y creativas
- **Respuesta estructurada**: JSON schema con:
  - `phrase`: Descripción de la palabra objetivo
  - `targetWord`: Palabra correcta
  - `synonyms`: Lista de respuestas alternativas aceptadas
  - `hint`: Pista adicional opcional
  - `languageCode`: Idioma de la respuesta
- **Adaptación**: Las pistas se ajustan automáticamente al nivel de dificultad y idioma del jugador

### Azure PlayFab
- **Autenticación**: Login automático con Custom ID basado en identificador único del dispositivo
- **Leaderboard global**: Clasificación de mejores puntuaciones sincronizada en tiempo real
- **Estadísticas**: Persistencia de `HighScore` del jugador en la nube
- **Perfiles**: Nombres personalizables para identificación en rankings
- **Sincronización automática**: Solo sube puntuaciones que superan el récord personal previo
