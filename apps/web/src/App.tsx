import { useState } from 'react'

export function App() {
  const [menuOpen, setMenuOpen] = useState(false)

  return (
    <div className="min-h-screen bg-slate-100 text-slate-900 md:flex">
      <a href="#conteudo" className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:bg-white focus:p-3">
        Ir para o conteúdo
      </a>
      <aside className="bg-blue-950 text-white md:min-h-screen md:w-64 md:shrink-0">
        <div className="flex min-h-20 items-center justify-between gap-4 px-6">
          <a href="#inicio" className="text-lg font-semibold tracking-tight">TMS Embarcador</a>
          <button
            type="button"
            className="rounded border border-blue-700 px-3 py-2 text-sm hover:bg-blue-900 md:hidden"
            aria-expanded={menuOpen}
            aria-controls="navegacao"
            onClick={() => setMenuOpen(!menuOpen)}
          >
            {menuOpen ? 'Fechar menu' : 'Abrir menu'}
          </button>
        </div>
        <nav id="navegacao" aria-label="Navegação principal" className={`${menuOpen ? 'block' : 'hidden'} px-4 pb-5 md:block`}>
          <a href="#inicio" aria-current="page" onClick={() => setMenuOpen(false)} className="block rounded-md bg-blue-800 px-4 py-3 text-sm font-medium">
            Início
          </a>
        </nav>
      </aside>
      <div className="min-w-0 flex-1">
        <header className="flex h-20 items-center border-b border-slate-200 bg-white px-6 md:px-10">
          <span className="text-sm font-medium text-slate-600">Início</span>
        </header>
        <main id="conteudo" tabIndex={-1} className="px-6 py-10 md:px-10 md:py-14">
          <section id="inicio" aria-labelledby="titulo" className="max-w-3xl">
            <h1 id="titulo" className="text-3xl font-semibold tracking-tight md:text-4xl">TMS Embarcador</h1>
            <p className="mt-4 text-lg leading-relaxed text-slate-600">Gestão de transporte para micro e pequenas empresas.</p>
            <p className="mt-8 border-l-4 border-blue-700 pl-4 text-sm leading-6 text-slate-600">O sistema está em desenvolvimento. Esta versão apresenta a estrutura inicial de navegação.</p>
          </section>
        </main>
      </div>
    </div>
  )
}
