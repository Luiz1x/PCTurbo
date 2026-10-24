// Tira capturas de tela do site montado, para eu conferir o visual
import { chromium } from 'playwright';
import fs from 'node:fs';

fs.mkdirSync('../shots', { recursive: true });
const browser = await chromium.launch();
for (const [name, w, h] of [['desktop', 1280, 900], ['mobile', 390, 844]]) {
  const page = await browser.newPage({ viewport: { width: w, height: h } });
  await page.goto('http://localhost:3000/PCTurbo/', { waitUntil: 'networkidle' });
  await page.screenshot({ path: `../shots/${name}.png`, fullPage: true });
}
await browser.close();
console.log('capturas feitas');
