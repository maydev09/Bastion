import js from "@eslint/js";
import prettier from "eslint-config-prettier";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import globals from "globals";
import tseslint from "typescript-eslint";

const boundary = (files, group, message) => ({
  files,
  rules: { "no-restricted-imports": ["error", { patterns: [{ group, message }] }] },
});

export default tseslint.config(
  {
    ignores: ["src/Web/Shared/api/schema.ts", "src/Host/**", "**/bin/**", "**/obj/**"],
  },
  js.configs.recommended,
  tseslint.configs.recommended,
  reactHooks.configs.flat.recommended,
  reactRefresh.configs.vite,
  { languageOptions: { globals: globals.browser } },
  boundary(
    ["src/Web/Modules/**"],
    ["@modules/**", "../*/**", "**/Modules/**", "**/App/**"],
    "A module UI imports only its own files and @shared.",
  ),
  boundary(
    ["src/Web/Shared/**"],
    ["@modules/**", "**/Modules/**", "**/App/**"],
    "Shared code knows no modules and no shell.",
  ),
  prettier,
);
