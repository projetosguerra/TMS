import { useState, type FormEvent } from 'react'
import { entrar as autenticar, obterUsuario, type UsuarioDaSessao } from './services/api'
import { DashboardScreen } from './screens/DashboardScreen'
import { FretesScreen } from './screens/FretesScreen'
import { TabelasScreen } from './screens/TabelasScreen'
import { TransportadorasScreen } from './screens/TransportadorasScreen'
import { input, label, Mensagem, primary } from './screens/ui'

type Secao = 'dashboard' | 'fretes' | 'transportadoras' | 'tabelas'
const secoes: { id: Secao; titulo: string }[] = [
  { id: 'dashboard', titulo: 'Painel' },
  { id: 'fretes', titulo: 'Fretes' },
  { id: 'transportadoras', titulo: 'Transportadoras' },
  { id: 'tabelas', titulo: 'Tabelas de frete' },
]

export function App() {
  const [token, setToken] = useState<string | null>(null)
  const [usuario, setUsuario] = useState<UsuarioDaSessao | null>(null)
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [entrando, setEntrando] = useState(false)
  const [erro, setErro] = useState('')
  const [secao, setSecao] = useState<Secao>('dashboard')
  const [menuAberto, setMenuAberto] = useState(false)
  const [transportadoraInicial, setTransportadoraInicial] = useState<string | null>(null)

  async function fazerLogin(e: FormEvent) {
    e.preventDefault(); setErro(''); setEntrando(true)
    try {
      const accessToken = await autenticar(email.trim(), senha)
      const perfil = await obterUsuario(accessToken)
      setToken(accessToken); setUsuario(perfil); setSenha(''); setSecao('dashboard')
    } catch (falha) {
      setSenha(''); setErro(falha instanceof Error ? falha.message : 'Não foi possível entrar no sistema.')
    } finally { setEntrando(false) }
  }
  function sair() { setToken(null); setUsuario(null); setSenha(''); setSecao('dashboard'); setErro('') }
  function abrirTabelas(id: string) { setTransportadoraInicial(id); setSecao('tabelas') }
  const titulo = secoes.find(x => x.id === secao)?.titulo ?? 'Painel'

  return <div className="min-h-screen bg-slate-100 text-slate-900 md:flex">
    <a href="#conteudo" className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:bg-white focus:p-3">Ir para o conteúdo</a>
    <aside className="bg-blue-950 text-white md:min-h-screen md:w-64 md:shrink-0">
      <div className="flex min-h-20 items-center justify-between gap-4 px-6"><span className="text-lg font-semibold tracking-tight">TMS Embarcador</span><button type="button" className="rounded border border-blue-700 px-3 py-2 text-sm hover:bg-blue-900 md:hidden" aria-expanded={menuAberto} aria-controls="navegacao" onClick={() => setMenuAberto(!menuAberto)}>{menuAberto ? 'Fechar menu' : 'Abrir menu'}</button></div>
      <nav id="navegacao" aria-label="Navegação principal" className={`${menuAberto ? 'block' : 'hidden'} space-y-1 px-4 pb-5 md:block`}>
        {secoes.map(item => <button key={item.id} type="button" disabled={!token} aria-current={secao === item.id ? 'page' : undefined} onClick={() => { setSecao(item.id); setMenuAberto(false) }} className={`block w-full rounded-md px-4 py-3 text-left text-sm font-medium disabled:cursor-not-allowed disabled:opacity-50 ${secao === item.id ? 'bg-blue-800' : 'hover:bg-blue-900'}`}>{item.titulo}</button>)}
      </nav>
    </aside>
    <div className="min-w-0 flex-1">
      <header className="flex min-h-20 items-center justify-between gap-4 border-b border-slate-200 bg-white px-6 md:px-10"><span className="text-sm font-medium text-slate-600">{token ? titulo : 'Acesso'}</span>{usuario && <div className="flex items-center gap-4 text-sm"><span className="hidden text-slate-600 sm:inline">{usuario.nome} · {usuario.empresa.nome}</span><button type="button" onClick={sair} className="font-medium text-blue-800 underline hover:text-blue-950">Sair</button></div>}</header>
      <main id="conteudo" tabIndex={-1} className="px-5 py-8 md:px-10 md:py-10">
        {!token ? <section className="mx-auto max-w-md rounded-lg border border-slate-200 bg-white p-6 shadow-sm" aria-labelledby="titulo-login"><h1 id="titulo-login" className="text-2xl font-semibold">Entrar no TMS</h1><p className="mt-2 text-sm text-slate-600">Use sua conta de desenvolvimento do Supabase Auth.</p><Mensagem texto={erro} /><form className="mt-6 space-y-4" onSubmit={fazerLogin}><div><label className={label} htmlFor="email-login">E-mail</label><input className={input} id="email-login" type="email" autoComplete="username" required value={email} onChange={e => setEmail(e.target.value)} /></div><div><label className={label} htmlFor="senha-login">Senha</label><input className={input} id="senha-login" type="password" autoComplete="current-password" required value={senha} onChange={e => setSenha(e.target.value)} /></div><button disabled={entrando} className={`${primary} w-full`}>{entrando ? 'Entrando…' : 'Entrar'}</button></form></section>
          : secao === 'dashboard' ? <DashboardScreen token={token} />
            : secao === 'fretes' ? <FretesScreen token={token} />
              : secao === 'transportadoras' ? <TransportadorasScreen token={token} abrirTabelas={abrirTabelas} />
                : <TabelasScreen token={token} transportadoraInicial={transportadoraInicial} />}
      </main>
    </div>
  </div>
}
