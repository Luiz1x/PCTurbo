import fs from 'node:fs';
import path from 'node:path';

export const REPO = 'https://github.com/Luiz1x/PCTurbo';
export const DOWNLOAD = REPO + '/releases/latest/download/PCTurbo-completo.zip';
export const RELEASES = REPO + '/releases';

// Versao atual: vem do version.txt do repositorio na hora de montar o site
export function getVersion() {
  try {
    return fs.readFileSync(path.join(process.cwd(), '..', 'version.txt'), 'utf8').replace(/^﻿/, '').trim();
  } catch {
    return '';
  }
}

// Novidades escritas para quem usa (as mais novas primeiro)
export const CHANGELOG = [
  {
    version: '2.2',
    items: [
      'Cartão de resultado em imagem para mandar aos amigos: lixo removido, programas, RAM, CPU parada, espaço livre e FPS.',
      'Histórico das otimizações com barras.',
    ],
  },
  {
    version: '2.1',
    items: [
      'Nova aba Jogos com Modo Jogo automático: quando o jogo abre, programas em segundo plano que pesam são colocados em prioridade baixa; quando fecha, tudo volta.',
      'Lista sua de programas que o PC Turbo nunca mexe.',
    ],
  },
  {
    version: '2.0',
    items: [
      'Vigia de programas: confere Epic, Steam, Riot, Discord e outros antes e depois, e avisa se algum sumir ou voltar para uma versão antiga.',
      '"Ver o que vai mudar": prévia completa, sem aplicar nada.',
      'Modo notebook: pula os ajustes de energia que gastam bateria.',
      'Removidos os recursos que podiam dar problema: atualizar programas pelo winget, remover apps da Microsoft e a aba Programas.',
    ],
  },
  {
    version: '1.9',
    items: [
      'Relatório de ganho (antes e depois) salvo em arquivo e aba Meu ganho.',
      'Teste de FPS opcional com o PresentMon.',
      'Atualização mais segura: só versões que o GitHub compilou e conferiu, com verificação do pacote (SHA-256).',
    ],
  },
];
