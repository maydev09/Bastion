import path from "node:path";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

const repo = import.meta.dirname;

export default defineConfig({
  root: path.join(repo, "src/Web/App"),
  plugins: [react()],
  resolve: {
    alias: {
      "@shared": path.join(repo, "src/Web/Shared"),
      "@modules": path.join(repo, "src/Modules"),
    },
  },
  server: {
    port: 5173,
    proxy: { "/api": "http://localhost:5080" },
    fs: { allow: [repo] },
  },
  build: {
    outDir: path.join(repo, "src/Host/Bastion.Host/wwwroot"),
    emptyOutDir: true,
  },
  test: {
    root: repo,
    include: ["src/**/*.test.{ts,tsx}"],
    environment: "jsdom",
    setupFiles: [path.join(repo, "src/Web/App/test/setup.ts")],
  },
});
