import { useEffect, useState } from 'react'
import { obterDashboard, type Dashboard } from '../services/api'
import { Cabecalho, input, label, Mensagem, panel, primary } from './ui'

const hoje = new Date().toISOString().slice(0, 10)
const inicioPadrao = new Date(Date.now() - 29 * 86400000).toISOString().slice(0, 10)
const dinheiro = (x: number) => x.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

export function DashboardScreen({ token }: { token: string }) {
  const [dados, setDados] = useState<Dashboard | null>(null)
  const [de, setDe] = useState(inicioPadrao)
  const [ate, setAte] = useState(hoje)
  const [carregando, setCarregando] = useState(false)
  const [erro, setErro] = useState('')

  async function carregar() {
    setCarregando(true); setErro('')
    try { setDados(await obterDashboard(token, de, ate)) }
    catch (e) { setErro(e instanceof Error ? e.message : 'Não foi possível carregar os indicadores.') }
    finally { setCarregando(false) }
  }
  useEffect(() => { void carregar() }, [token])

  return <section className="mx-auto max-w-6xl">
    <Cabecalho titulo="Painel" descricao="Indicadores calculados a partir dos fretes da sua empresa." />
    <Mensagem texto={erro} />
    <form className={`${panel} mt-6 flex flex-wrap items-end gap-4 p-5`} onSubmit={e => { e.preventDefault(); void carregar() }}>
      <div><label className={label} htmlFor="painel-de">De</label><input className={input} id="painel-de" type="date" required value={de} onChange={e => setDe(e.target.value)} /></div>
      <div><label className={label} htmlFor="painel-ate">Até</label><input className={input} id="painel-ate" type="date" required value={ate} onChange={e => setAte(e.target.value)} /></div>
      <button className={primary} disabled={carregando}>{carregando ? 'Atualizando…' : 'Atualizar indicadores'}</button>
    </form>
    {carregando && !dados ? <p className="py-8 text-sm text-slate-600">Carregando indicadores…</p> : dados && <>
      <dl className="mt-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Indicador titulo="Fretes ativos" valor={String(dados.fretesAtivos)} />
        <Indicador titulo="Entregues no período" valor={String(dados.entreguesNoPeriodo)} />
        <Indicador titulo="Entregues no prazo" valor={`${dados.percentualNoPrazo}%`} />
        <Indicador titulo="Fretes atrasados" valor={String(dados.fretesAtrasados)} />
        <Indicador titulo="Custo total no período" valor={dinheiro(dados.custoTotal)} />
        <Indicador titulo="Custo médio por entrega" valor={dinheiro(dados.custoMedioPorFrete)} />
        <Indicador titulo="Tempo médio até a entrega" valor={`${dados.tempoMedioEntregaDias} dias`} />
        <Indicador titulo="Período consultado" valor={`${dados.de} a ${dados.ate}`} />
      </dl>
      <div className={`${panel} mt-6 overflow-hidden`}><div className="border-b border-slate-200 px-5 py-4"><h2 className="font-semibold">Desempenho por transportadora</h2></div>{dados.rankingTransportadoras.length === 0 ? <p className="px-5 py-8 text-sm text-slate-600">Ainda não há entregas no período selecionado.</p> : <div className="overflow-x-auto"><table className="min-w-full text-left text-sm"><thead className="bg-slate-50 text-xs uppercase text-slate-600"><tr><th className="px-5 py-3">Transportadora</th><th className="px-5 py-3">Entregas</th><th className="px-5 py-3">Custo médio</th><th className="px-5 py-3">No prazo</th></tr></thead><tbody className="divide-y divide-slate-200">{dados.rankingTransportadoras.map(x => <tr key={x.transportadoraId}><td className="px-5 py-4 font-medium">{x.transportadora}</td><td className="px-5 py-4">{x.quantidade}</td><td className="px-5 py-4">{dinheiro(x.custoMedio)}</td><td className="px-5 py-4">{x.percentualNoPrazo}%</td></tr>)}</tbody></table></div>}</div>
    </>}
  </section>
}

function Indicador({ titulo, valor }: { titulo: string; valor: string }) {
  return <div className={`${panel} p-5`}><dt className="text-sm text-slate-600">{titulo}</dt><dd className="mt-2 text-2xl font-semibold text-slate-900">{valor}</dd></div>
}
