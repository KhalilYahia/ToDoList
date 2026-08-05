import createNextIntlPlugin from "next-intl/plugin";
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  reactStrictMode: true,
};

/*
const memoryMonitor = globalThis as typeof globalThis & {
  __memoryMonitorStarted?: boolean;
};

if (
  process.env.NODE_ENV === "development" &&
  !memoryMonitor.__memoryMonitorStarted
) {
  memoryMonitor.__memoryMonitorStarted = true;

  setInterval(() => {
    const memory = process.memoryUsage();

    console.log("[Node memory]", {
      rssMB: Math.round(memory.rss / 1024 / 1024),
      heapUsedMB: Math.round(memory.heapUsed / 1024 / 1024),
      heapTotalMB: Math.round(memory.heapTotal / 1024 / 1024),
      externalMB: Math.round(memory.external / 1024 / 1024),
      arrayBuffersMB: Math.round(memory.arrayBuffers / 1024 / 1024),
    });
  }, 10_000).unref();
}*/

export default createNextIntlPlugin("./src/i18n/request.ts")(nextConfig);
