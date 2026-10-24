// Site estatico (sem servidor): o GitHub Pages so serve arquivos.
const nextConfig = {
  output: 'export',
  basePath: '/PCTurbo',
  trailingSlash: true,
  images: { unoptimized: true },
};

export default nextConfig;
