import './globals.css';

export const metadata = {
  title: 'PC Turbo: seu PC mais leve em 1 clique',
  description:
    'Limpa o lixo, ajusta o Windows e mostra o antes e depois, sem remover nada que você usa. Grátis, para Windows.',
};

export default function RootLayout({ children }) {
  return (
    <html lang="pt-BR">
      <body>{children}</body>
    </html>
  );
}
