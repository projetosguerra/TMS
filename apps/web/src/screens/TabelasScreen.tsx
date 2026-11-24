import { useEffect, useState, type FormEvent } from 'react'
import { atualizarTabela, criarTabela, listarTabelas, listarTransportadoras, type TabelaFrete, type TabelaFreteEntrada, type Transportadora } from '../services/api'
import { Cabecalho, input, label, Mensagem, panel, primary, secondary } from './ui'

const vazio: TabelaFreteEntrada = { ufOrigem: 'SP', ufDestino: '', valorMinimo: 0, valorPorKg: 0, prazoDias: 1, ativa: true }

export function TabelasScreen({ token, transportadoraInicial }: { token: string; transportadoraInicial: string | null }) {
  const [parceiros, setParceiros] = useState<Transportadora[]>([])
  const [parceiroId, setParceiroId] = useState(transportadoraInicial ?? '')
  const [tabelas, setTabelas] = useState<TabelaFrete[]>([])
  const [formulario, setFormulario] = useState<TabelaFreteEntrada>(vazio)
  const [edicao, setEdicao] = useState<string | null>(null)
  const [aberto, setAberto] = useState(false)
  const [erro, setErro] = useState('')
  const [sucesso, setSucesso] = useState('')
  const [carregando, setCarregando] = useState(false)
  const [salvando, setSalvando] = useState(false)

  async function carregarParceiros() {
    try {
      const ativos = (await listarTransportadoras(token)).filter(x => x.ativo)
      setParceiros(ativos)
      if (!parceiroId && ativos.length) setParceiroId(ativos[0].id)
    } catch (e) { setErro(e instanceof Error ? e.message : 'Não foi possível carregar as transportadoras.') }
  }
  async function carregarTabelas(id = parceiroId) {
    if (!id) { setTabelas([]); return }
    setCarregando(true)
    try { setTabelas(await listarTabelas(token, id)); setErro('') }
    catch (e) { setErro(e instanceof Error ? e.message : 'Não foi possível carregar as tabelas.') }
    finally { setCarregando(false) }
  }
  useEffect(() => { void carregarParceiros() }, [token])
  useEffect(() => { void carregarTabelas(parceiroId) }, [token, parceiroId])

  function editar(tabela: TabelaFrete) {
    setEdicao(tabela.id)
    setFormulario({ ufOrigem: tabela.ufOrigem, ufDestino: tabela.ufDestino, valorMinimo: tabela.valorMinimo, valorPorKg: tabela.valorPorKg, prazoDias: tabela.prazoDias, ativa: tabela.ativa })
    setAberto(true); setErro(''); setSucesso('')
  }
  function nova() { setEdicao(null); setFormulario(vazio); setAberto(true); setErro(''); setSucesso('') }
  async function salvar(e: FormEvent) {
    e.preventDefault()
    if (!parceiroId) return
    setSalvando(true); setErro(''); setSucesso('')
    try {
      if (edicao) { await atualizarTabela(token, edicao, formulario); setSucesso('Tabela atualizada.') }
      else { await criarTabela(token, parceiroId, formulario); setSucesso('Tabela criada.') }
      setAberto(false); await carregarTabelas()
    } catch (falha) { setErro(falha instanceof Error ? falha.message : 'Não foi possível salvar a tabela.') }
    finally { setSalvando(false) }
  }

  return <section className="mx-auto max-w-6xl">
    <Cabecalho titulo="Tabelas de frete" descricao="Configure tarifas por rota para calcular cotações." acao={<button disabled={!parceiroId} className={primary} onClick={nova}>Nova tabela</button>} />
    <Mensagem texto={erro} /><Mensagem texto={sucesso} sucesso />
    <div className={`${panel} mt-6 p-5`}><label className={label} htmlFor="transportadora-tabela">Transportadora</label><select id="transportadora-tabela" className={`${input} max-w-xl`} value={parceiroId} onChange={e => setParceiroId(e.target.value)}><option value="">Selecione</option>{parceiros.map(x => <option key={x.id} value={x.id}>{x.nome}</option>)}</select>{parceiros.length === 0 && <p className="mt-3 text-sm text-slate-600">Cadastre uma transportadora ativa antes de configurar tarifas.</p>}</div>
    {aberto && <form onSubmit={salvar} className={`${panel} mt-5 p-5`}>
      <h2 className="text-lg font-semibold">{edicao ? 'Editar tabela' : 'Nova tabela'}</h2>
      <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <div><label className={label} htmlFor="uf-origem">UF de origem</label><input className={input} id="uf-origem" maxLength={2} minLength={2} required value={formulario.ufOrigem} onChange={e => setFormulario({ ...formulario, ufOrigem: e.target.value.toUpperCase() })} /></div>
        <div><label className={label} htmlFor="uf-destino">UF de destino</label><input className={input} id="uf-destino" maxLength={2} minLength={2} required value={formulario.ufDestino} onChange={e => setFormulario({ ...formulario, ufDestino: e.target.value.toUpperCase() })} /></div>
        <div><label className={label} htmlFor="valor-minimo">Valor mínimo (R$)</label><input className={input} id="valor-minimo" type="number" min="0.01" step="0.01" required value={formulario.valorMinimo || ''} onChange={e => setFormulario({ ...formulario, valorMinimo: Number(e.target.value) })} /></div>
        <div><label className={label} htmlFor="valor-quilo">Valor por kg (R$)</label><input className={input} id="valor-quilo" type="number" min="0.0001" step="0.0001" required value={formulario.valorPorKg || ''} onChange={e => setFormulario({ ...formulario, valorPorKg: Number(e.target.value) })} /></div>
        <div><label className={label} htmlFor="prazo-tabela">Prazo (dias)</label><input className={input} id="prazo-tabela" type="number" min="1" step="1" required value={formulario.prazoDias} onChange={e => setFormulario({ ...formulario, prazoDias: Number(e.target.value) })} /></div>
        <div className="flex items-end pb-2"><label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={formulario.ativa} onChange={e => setFormulario({ ...formulario, ativa: e.target.checked })} />Tabela ativa</label></div>
      </div>
      <div className="mt-5 flex gap-3"><button disabled={salvando} className={primary}>{salvando ? 'Salvando…' : 'Salvar tabela'}</button><button type="button" className={secondary} onClick={() => setAberto(false)}>Cancelar</button></div>
    </form>}
    <div className={`${panel} mt-5 overflow-hidden`}>
      <div className="border-b border-slate-200 px-5 py-4"><h2 className="font-semibold">Tarifas cadastradas</h2></div>
      {carregando ? <p className="px-5 py-8 text-sm text-slate-600">Carregando tabelas…</p> : tabelas.length === 0 ? <p className="px-5 py-8 text-sm text-slate-600">Nenhuma tabela de frete cadastrada para esta transportadora.</p> : <div className="overflow-x-auto"><table className="min-w-full text-left text-sm"><thead className="bg-slate-50 text-xs uppercase text-slate-600"><tr>{['Rota', 'Mínimo', 'Valor/kg', 'Prazo', 'Situação', 'Ação'].map(x => <th key={x} className="px-5 py-3">{x}</th>)}</tr></thead><tbody className="divide-y divide-slate-200">{tabelas.map(x => <tr key={x.id}><td className="px-5 py-4">{x.ufOrigem} → {x.ufDestino}</td><td className="px-5 py-4">{x.valorMinimo.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}</td><td className="px-5 py-4">{x.valorPorKg.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL', minimumFractionDigits: 4 })}</td><td className="px-5 py-4">{x.prazoDias} dias</td><td className="px-5 py-4">{x.ativa ? 'Ativa' : 'Inativa'}</td><td className="px-5 py-4"><button className="text-blue-800 underline" onClick={() => editar(x)}>Editar</button></td></tr>)}</tbody></table></div>}
    </div>
  </section>
}
