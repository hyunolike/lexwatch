import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  cacheComponents: true,
  partialPrefetching: true,
  // CSS 로더의 이전 산출물이 배포 캐시에서 되살아난 문제를 피한다.
  // 최종 CSS 검사가 별도로 실행되며 개발 서버 캐시는 유지한다.
  experimental: { turbopackFileSystemCacheForBuild: false },
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
