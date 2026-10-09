// Reuse the sibling SeatSheet project's dependencies and frontend without changing its config.
import { createServer } from '../../SeatSheet/node_modules/vite/dist/node/index.js';
import { fileURLToPath } from 'node:url';
const server = await createServer({
  root: fileURLToPath(new URL('../../SeatSheet/frontend', import.meta.url)),
  server: { host: '127.0.0.1', port: 5173, strictPort: true, proxy: { '/api': 'http://127.0.0.1:3000' } },
  define: { 'import.meta.env.VITE_API_BASE_URL': JSON.stringify('') }
});
await server.listen();
server.printUrls();
