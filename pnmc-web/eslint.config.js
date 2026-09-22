// @ts-check
const eslint = require("@eslint/js");
const { defineConfig } = require("eslint/config");
const tseslint = require("typescript-eslint");
const angular = require("angular-eslint");

/**
 * ESLint se incorporo sobre un proyecto ya escrito, que arranco con 750
 * hallazgos. Bloquear la integracion con todos ellos habria dejado el CI en
 * rojo permanente, que es lo mismo que no tener CI.
 *
 * El criterio es por naturaleza del hallazgo, no por cantidad:
 *
 *   error  Lo que denota un defecto real — codigo muerto, variables sin usar,
 *          expresiones sin efecto, ciclos de vida vacios. Rompe el CI.
 *   warn   Deuda conocida y acotada — el tipado `any`. Se ve en cada corrida y
 *          se paga por fases, sin frenar el trabajo en curso.
 *
 * Al saldar cada bloque, subir su regla a "error" para que no reaparezca.
 *
 * TIPADO `any` — EN "warn", PERO YA NO SIN VIGILANCIA (PNMC-058).
 * Un aviso que nadie cuenta no frena nada: la deuda podia subir sin que nada
 * fallara. Quien la cuenta ahora es `trinquete/trinquete-any.mjs`, que rompe
 * el CI si la cifra sube por encima del techo sellado en
 * `trinquete/techo-any.json`.
 *
 * La cifra oficial son OCURRENCIAS de texto, no avisos de ESLint — son dos
 * medidas distintas y mezclarlas es lo que produjo cuatro numeros para el
 * mismo hecho. La oficial es:
 *
 *     grep -roE ':\s*any\b|<any>|as any|any\[\]' --include='*.ts' src | wc -l
 *
 * Valor remedido de cero el 2026-08-22T04:30:29Z (21 ago 23:30 hora local):
 * **533** en 105 ficheros. El comentario que habia aqui decia 553 y el brief
 * de PNMC-058 traia 526; ninguno de los dos era ya cierto. Si tocas esta
 * seccion, remide y sella la hora: una cifra sin hora, en este repositorio,
 * no vale.
 *
 * Esta regla NO se sube a "error" todavia a proposito: con 533 casos dejaria
 * el CI en rojo permanente, que es lo mismo que no tener CI. El trinquete da
 * el efecto de "error" para lo nuevo sin el coste de lo heredado.
 *
 * ACCESIBILIDAD — SALDADA el 21 ago 2026 y ya en "error".
 * Eran 99 avisos, y la mitad estaba en el sitio publico, no en la consola
 * interna: obligacion legal de un sitio del Estado, no pulido interno. Se
 * corrigieron los 99 y se comprobo con el teclado de verdad
 * (`e2e/teclado-sitio-publico.spec.ts`), porque que ESLint se calle solo prueba
 * que no hay avisos, no que alguien pueda usar el sitio sin raton.
 *
 * Donde el silencio es correcto —el fondo de un modal, que no es un control—
 * hay un eslint-disable-next-line con la razon escrita al lado. No se suprimen
 * en bloque desde aqui: cada excepcion se justifica donde vive.
 */
module.exports = defineConfig([
  {
    files: ["**/*.ts"],
    extends: [
      eslint.configs.recommended,
      tseslint.configs.recommended,
      tseslint.configs.stylistic,
      angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      "@angular-eslint/directive-selector": [
        "error",
        {
          type: "attribute",
          prefix: "app",
          style: "camelCase",
        },
      ],
      "@angular-eslint/component-selector": [
        "error",
        {
          type: "element",
          prefix: "app",
          style: "kebab-case",
        },
      ],

      // --- Deuda de tipado heredada. Se paga por fases. ---
      // El techo lo vigila trinquete/trinquete-any.mjs (533 al 2026-08-22).
      // Bajarla de "warn" a "off" para silenciar la corrida deja al trinquete
      // como unica red: no lo hagas sin mirarlo.
      "@typescript-eslint/no-explicit-any": "warn",

      // --- Convenciones Angular saldadas. ---
      "@angular-eslint/prefer-inject": "error",
      "@angular-eslint/no-output-on-prefix": "error",

      // --- Defectos reales: rompen el CI. ---
      // Una variable o import sin usar suele ser un rastro de codigo muerto o
      // de un renombrado a medias.
      "@typescript-eslint/no-unused-vars": ["error", { argsIgnorePattern: "^_" }],
      "@typescript-eslint/no-empty-function": "error",
      "@angular-eslint/no-empty-lifecycle-method": "error",
      "no-useless-escape": "error",
      "no-useless-assignment": "error",
      "prefer-const": "error",
    },
  },
  {
    files: ["**/*.html"],
    extends: [
      angular.configs.templateRecommended,
      angular.configs.templateAccessibility,
    ],
    rules: {
      // --- Accesibilidad: 103 casos heredados. ---
      // Es un sitio publico del Estado, asi que esto es deuda a saldar, no a
      // descartar; queda visible en cada corrida hasta que se corrija.
      // Saldadas: en "error" para que no reaparezcan. Bajarlas a "warn" para
      // "pasar el CI" devuelve el sitio al estado que costo un dia arreglar.
      "@angular-eslint/template/label-has-associated-control": "error",
      "@angular-eslint/template/click-events-have-key-events": "error",
      "@angular-eslint/template/interactive-supports-focus": "error",
    },
  },
  {
    // El codigo generado y los artefactos de build no se revisan.
    ignores: ["dist/**", "node_modules/**", ".angular/**", "coverage/**"],
  },
]);
