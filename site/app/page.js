import { CHANGELOG, DOWNLOAD, RELEASES, REPO, getVersion } from '../lib/data';

const steps = [
  {
    n: '1',
    title: 'Baixe e extraia',
    text: 'Baixe o ZIP, clique com o botão direito e escolha "Extrair tudo". Não precisa instalar nada.',
  },
  {
    n: '2',
    title: 'Clique em OTIMIZAR TUDO',
    text: 'Abra o INICIAR.bat e aceite o pedido de administrador. Um clique faz tudo, sem perguntas.',
  },
  {
    n: '3',
    title: 'Reinicie e veja o resultado',
    text: 'Na aba Meu ganho você vê o antes e o depois do seu PC, com números reais medidos nele.',
  },
];

const features = [
  {
    icon: '🧹',
    title: 'Limpeza segura',
    text: 'Remove lixo temporário, caches e a Lixeira. Não apaga jogos, fotos, downloads nem documentos.',
  },
  {
    icon: '⚙️',
    title: 'Ajustes que você desfaz',
    text: 'Cria um ponto de restauração antes e tem um botão para desfazer os ajustes do Windows.',
  },
  {
    icon: '🛡️',
    title: 'Vigia de programas',
    text: 'Confere Epic, Steam, Riot, Discord e outros antes e depois. Se algum sumir ou voltar para uma versão antiga, avisa.',
  },
  {
    icon: '🎮',
    title: 'Modo Jogo automático',
    text: 'Quando o jogo abre, programas em segundo plano que pesam ficam em prioridade baixa. Quando fecha, tudo volta.',
  },
  {
    icon: '📊',
    title: 'Meu ganho',
    text: 'Programas rodando, RAM, CPU parada, espaço liberado e histórico, mais um cartão em imagem para mandar aos amigos.',
  },
  {
    icon: '⏱️',
    title: 'Teste de FPS opcional',
    text: 'Mede FPS médio e 1% low do jogo antes e depois, para provar o ganho com número. Só roda quando você pede.',
  },
];

const never = [
  'Riot e Vanguard',
  'Xbox e Game Pass',
  'Seus jogos e launchers (Epic, Steam e outros)',
  'Seus arquivos e documentos',
  'Seus programas instalados',
  'Drivers de vídeo e do sistema',
];

const faq = [
  {
    q: 'Ele remove programas ou jogos?',
    a: 'Não. O OTIMIZAR TUDO só limpa lixo temporário (caches e a Lixeira, que é só o que você já apagou) e aplica ajustes do Windows que podem ser desfeitos. Ele não desinstala nada.',
  },
  {
    q: 'Dá para desfazer?',
    a: 'Sim. Ele cria um ponto de restauração antes e o Painel tem o botão "Desfazer ajustes do Otimizar". Os arquivos de lixo já limpos não voltam, porque eram lixo.',
  },
  {
    q: 'Quanto de FPS eu vou ganhar?',
    a: 'Não prometemos número nenhum. Depende do seu PC e do jogo. O app mede o antes e o depois no seu próprio computador (aba Meu ganho e Teste de FPS) e mostra o resultado como ele é, inclusive quando não melhora.',
  },
  {
    q: 'Por que o Windows avisa que o programa é desconhecido?',
    a: 'Porque o app ainda não tem assinatura digital paga. Se o SmartScreen aparecer, clique em "Mais informações" e depois em "Executar assim mesmo". Se os arquivos vierem bloqueados, clique com o botão direito no ZIP, vá em Propriedades e marque "Desbloquear" antes de extrair. O código é aberto no GitHub, e cada versão é compilada e testada pelo próprio GitHub antes de ser publicada.',
  },
  {
    q: 'Funciona em notebook?',
    a: 'Sim. Se o PC tiver bateria, o app pula o plano de energia máximo e o ajuste de USB e PCIe, que gastam bateria.',
  },
  {
    q: 'Precisa de placa NVIDIA?',
    a: 'Não. As configurações de FPS da NVIDIA só são aplicadas se existir uma placa NVIDIA. Todo o resto funciona em qualquer PC com Windows.',
  },
  {
    q: 'É seguro com Valorant e Vanguard?',
    a: 'O PC Turbo não mexe na Riot nem no Vanguard. O Teste de FPS opcional usa o PresentMon, da Intel, que só lê os quadros pelo Windows. A Riot não publica uma lista oficial de programas permitidos, por isso esse teste é opcional e nunca roda sozinho.',
  },
  {
    q: 'O app envia meus dados para a internet?',
    a: 'Não. Ele só consulta o GitHub para ver se existe versão nova. O PresentMon só é baixado se você pedir o Teste de FPS. Os relatórios ficam no seu PC.',
  },
  {
    q: 'Como atualizo?',
    a: 'Pelo botão Atualizar dentro do app. Ele só oferece versões que o GitHub já compilou e confere o pacote antes de instalar.',
  },
];

export default function Home() {
  const version = getVersion();

  return (
    <>
      <header className="nav">
        <div className="container nav-inner">
          <a href="#topo" className="brand">
            <span className="bolt" aria-hidden="true">⚡</span> PC Turbo
          </a>
          <nav className="nav-links" aria-label="Seções">
            <a href="#como">Como funciona</a>
            <a href="#recursos">Recursos</a>
            <a href="#seguranca">Segurança</a>
            <a href="#novidades">Novidades</a>
            <a href="#faq">Perguntas</a>
            <a href={REPO} className="nav-gh">GitHub</a>
          </nav>
        </div>
      </header>

      <main id="topo">
        <section className="hero">
          <div className="container hero-grid">
            <div>
              <p className="eyebrow">Para Windows · grátis · sem instalador</p>
              <h1>
                Seu PC mais leve em <span className="hl">1 clique</span>.
              </h1>
              <p className="lead">
                Limpa o lixo, ajusta o Windows e mostra o antes e o depois, sem remover nada que você usa.
              </p>
              <div className="cta">
                <a className="btn btn-primary" href={DOWNLOAD}>
                  Baixar para Windows{version ? ' (v' + version + ')' : ''}
                </a>
                <a className="btn btn-ghost" href={REPO}>Ver o código no GitHub</a>
              </div>
              <p className="small muted">
                Precisa de Windows 11 (foi pensado nele) e de permissão de administrador. O Windows 10 abre, mas
                alguns ajustes são do 11.
              </p>
            </div>

            <div className="mock" aria-label="Ilustração do aplicativo">
              <div className="mock-bar">
                <span className="dot" /> <span className="dot" /> <span className="dot" />
                <span className="mock-title">PC Turbo</span>
              </div>
              <div className="mock-body">
                <div className="mock-btn">OTIMIZAR TUDO (1 clique)</div>
                <ul className="mock-list">
                  <li>Ponto de restauração criado</li>
                  <li>Lixo temporário e Lixeira limpos</li>
                  <li>Ajustes do Windows aplicados</li>
                  <li>Programas conferidos: tudo igual ao de antes</li>
                  <li>Resultado guardado em Meu ganho</li>
                </ul>
                <p className="small muted mock-note">Ilustração. Os números reais aparecem no seu PC.</p>
              </div>
            </div>
          </div>
        </section>

        <section id="como" className="section">
          <div className="container">
            <h2>Como funciona</h2>
            <div className="grid3">
              {steps.map((s) => (
                <div className="card" key={s.n}>
                  <div className="num">{s.n}</div>
                  <h3>{s.title}</h3>
                  <p className="muted">{s.text}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section id="recursos" className="section alt">
          <div className="container">
            <h2>O que ele faz</h2>
            <div className="grid3">
              {features.map((f) => (
                <div className="card" key={f.title}>
                  <div className="icon" aria-hidden="true">{f.icon}</div>
                  <h3>{f.title}</h3>
                  <p className="muted">{f.text}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section id="seguranca" className="section">
          <div className="container two">
            <div className="safe">
              <h2>O que ele nunca mexe</h2>
              <ul className="ticks">
                {never.map((n) => (
                  <li key={n}>{n}</li>
                ))}
              </ul>
              <p className="muted">
                Depois de otimizar, o app só confere se esses programas continuam iguais. Ele não altera nada neles.
              </p>
            </div>
            <div className="honest">
              <h2>Sem promessa mágica</h2>
              <p>
                Nenhum programa deixa um PC fraco com a velocidade de um PC forte. O que o PC Turbo faz é tirar peso
                desnecessário e provar o resultado com números do seu próprio PC.
              </p>
              <p className="muted">
                Se algo não melhorar, o relatório mostra isso também. O ganho em jogo você mede com o Teste de FPS:
                mesmo jogo, mesmo lugar, antes e depois.
              </p>
            </div>
          </div>
        </section>

        <section id="novidades" className="section alt">
          <div className="container">
            <h2>Novidades</h2>
            <div className="changelog">
              {CHANGELOG.map((c) => (
                <div className="card" key={c.version}>
                  <h3>Versão {c.version}</h3>
                  <ul>
                    {c.items.map((i) => (
                      <li key={i}>{i}</li>
                    ))}
                  </ul>
                </div>
              ))}
            </div>
            <p className="small muted">
              Todas as versões e arquivos estão em <a className="link" href={RELEASES}>Releases</a> no GitHub.
            </p>
          </div>
        </section>

        <section id="faq" className="section">
          <div className="container narrow">
            <h2>Perguntas frequentes</h2>
            {faq.map((f) => (
              <details className="faq" key={f.q}>
                <summary>{f.q}</summary>
                <p className="muted">{f.a}</p>
              </details>
            ))}
          </div>
        </section>

        <section className="final">
          <div className="container center">
            <h2>Pronto para deixar o PC mais leve?</h2>
            <a className="btn btn-primary" href={DOWNLOAD}>
              Baixar para Windows{version ? ' (v' + version + ')' : ''}
            </a>
          </div>
        </section>
      </main>

      <footer className="footer">
        <div className="container">
          <span>PC Turbo · projeto de Luiz Felipe</span>
          <a className="link" href={REPO}>Código no GitHub</a>
        </div>
      </footer>
    </>
  );
}
