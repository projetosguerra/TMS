import { useEffect, useMemo, useState, type FormEvent } from 'react'
import {
  alterarStatusFrete, atualizarFrete, criarDocumentoFiscal, criarFrete, listarFretes,
  listarTransportadoras, obterFrete, registrarProposta, removerProposta, selecionarProposta,
  simularCotacao, type DadosFrete, type DocumentoFiscal, type FreteDetalhe, type FreteEntrada,
  type FreteListaItem, type Transportadora,
} from '../services/api'
import { Cabecalho, input, label, Mensagem, panel, primary, secondary } from './ui'

const freteVazio: FreteEntrada = {
  origemCep: '', origemCidade: '', origemUf: 'SP', destinoCep: '', destinoCidade: '', destinoUf: '',
  destinatarioNome: '', pesoKg: 0, volumeM3: 0, valorMercadoria: null, observacao: null,
}
const statusLabels: Record<string, string> = { ABERTO: 'Aberto', EM_COTACAO: 'Em cotação', CONTRATADO: 'Contratado', COLETADO: 'Coletado', EM_TRANSITO: 'Em trânsito', ENTREGUE: 'Entregue', CANCELADO: 'Cancelado' }
const dinheiro = (valor: number | null) => valor == null ? '—' : valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
const somenteDigitos = (valor: string) => valor.replace(/\D/g, '').slice(0, 8)

export function FretesScreen({ token }: { token: string }) {
  const [itens, setItens] = useState<FreteListaItem[]>([])
  const [parceiros, setParceiros] = useState<Transportadora[]>([])
  const [pagina, setPagina] = useState(1)
  const [total, setTotal] = useState(0)
  const [busca, setBusca] = useState('')
  const [statusFiltro, setStatusFiltro] = useState('')
  const [somenteAtrasados, setSomenteAtrasados] = useState(false)
  const [carregando, setCarregando] = useState(false)
  const [idAberto, setIdAberto] = useState<string | null>(null)
  const [detalhe, setDetalhe] = useState<FreteDetalhe | null>(null)
  const [carregandoDetalhe, setCarregandoDetalhe] = useState(false)
  const [formAberto, setFormAberto] = useState(false)
  const [idEdicao, setIdEdicao] = useState<string | null>(null)
  const [formulario, setFormulario] = useState<FreteEntrada>(freteVazio)
  const [propostaParceiro, setPropostaParceiro] = useState('')
  const [propostaValor, setPropostaValor] = useState(0)
  const [propostaPrazo, setPropostaPrazo] = useState(1)
  const [propostaObservacao, setPropostaObservacao] = useState('')
  const [docTipo, setDocTipo] = useState<'NFE' | 'CTE' | 'OUTRO'>('NFE')
  const [docNumero, setDocNumero] = useState('')
  const [docChave, setDocChave] = useState('')
  const [docValor, setDocValor] = useState('')
  const [erro, setErro] = useState('')
  const [sucesso, setSucesso] = useState('')
  const [salvando, setSalvando] = useState(false)

  async function carregarLista(alvoPagina = pagina) {
    setCarregando(true)
    try {
      const resultado = await listarFretes(token, { status: statusFiltro, busca, atrasados: somenteAtrasados, pagina: alvoPagina, tamanho: 20 })
      setItens(resultado.itens); setTotal(resultado.total); setPagina(resultado.pagina); setErro('')
    } catch (e) { setErro(e instanceof Error ? e.message : 'Não foi possível carregar os fretes.') }
    finally { setCarregando(false) }
  }
  async function carregarDetalhe(id = idAberto) {
    if (!id) return
    setCarregandoDetalhe(true)
    try { setDetalhe(await obterFrete(token, id)); setErro('') }
    catch (e) { setErro(e instanceof Error ? e.message : 'Não foi possível abrir o frete.') }
    finally { setCarregandoDetalhe(false) }
  }
  useEffect(() => { void carregarLista(1); listarTransportadoras(token).then(x => { const ativos = x.filter(p => p.ativo); setParceiros(ativos); setPropostaParceiro(ativos[0]?.id ?? '') }).catch(() => undefined) }, [token])
  useEffect(() => { if (idAberto) void carregarDetalhe(idAberto) }, [idAberto, token])

  function novo() { setIdEdicao(null); setFormulario(freteVazio); setFormAberto(true); setErro(''); setSucesso('') }
  function editar(item: FreteDetalhe) { setIdEdicao(item.id); setFormulario({ ...item.dados }); setFormAberto(true); setErro(''); setSucesso('') }
  async function salvarFrete(e: FormEvent) {
    e.preventDefault(); setSalvando(true); setErro(''); setSucesso('')
    const entrada = { ...formulario, origemCep: somenteDigitos(formulario.origemCep), destinoCep: somenteDigitos(formulario.destinoCep), origemUf: formulario.origemUf.toUpperCase(), destinoUf: formulario.destinoUf.toUpperCase(), valorMercadoria: formulario.valorMercadoria || null, observacao: formulario.observacao?.trim() || null }
    try {
      const salvo = idEdicao ? await atualizarFrete(token, idEdicao, entrada) : await criarFrete(token, entrada)
      setFormAberto(false); setSucesso(idEdicao ? 'Frete atualizado.' : `Frete ${salvo.codigo} criado.`); setIdAberto(salvo.id); await carregarLista()
    } catch (e) { setErro(e instanceof Error ? e.message : 'Não foi possível salvar o frete.') }
    finally { setSalvando(false) }
  }
  async function agir(acao: () => Promise<unknown>, mensagem: string) {
    if (!detalhe) return
    setErro(''); setSucesso('')
    try { await acao(); setSucesso(mensagem); await carregarDetalhe(detalhe.id); await carregarLista() }
    catch (e) { setErro(e instanceof Error ? e.message : 'Não foi possível concluir a ação.') }
  }
  async function cancelarFrete() {
    if (!detalhe) return
    const motivo = window.prompt('Informe o motivo do cancelamento:')
    if (!motivo?.trim()) return
    await agir(() => alterarStatusFrete(token, detalhe.id, 'CANCELADO', motivo.trim()), 'Frete cancelado.')
  }
  async function adicionarProposta(e: FormEvent) {
    e.preventDefault()
    if (!detalhe) return
    await agir(() => registrarProposta(token, detalhe.id, { transportadoraId: propostaParceiro, valor: propostaValor, prazoDias: propostaPrazo, observacao: propostaObservacao.trim() || undefined }), 'Proposta registrada.')
    setPropostaValor(0); setPropostaPrazo(1); setPropostaObservacao('')
  }
  async function adicionarDocumento(e: FormEvent) {
    e.preventDefault()
    if (!detalhe) return
    const entrada: { tipo: string; numero: string; chaveAcesso?: string; valor?: number } = { tipo: docTipo, numero: docNumero.trim() }
    if (docTipo === 'NFE') entrada.chaveAcesso = docChave.replace(/\D/g, '')
    if (docValor) entrada.valor = Number(docValor)
    await agir(() => criarDocumentoFiscal(token, detalhe.id, entrada), 'Documento fiscal registrado.')
    setDocNumero(''); setDocChave(''); setDocValor('')
  }
  const maisBarata = useMemo(() => detalhe?.propostas.length ? [...detalhe.propostas].sort((a, b) => a.valor - b.valor)[0].id : '', [detalhe])
  const maisRapida = useMemo(() => detalhe?.propostas.length ? [...detalhe.propostas].sort((a, b) => a.prazoDias - b.prazoDias)[0].id : '', [detalhe])
  const podeCotacao = detalhe?.status === 'ABERTO' || detalhe?.status === 'EM_COTACAO'
  const podeEscolher = detalhe?.status === 'EM_COTACAO' || detalhe?.status === 'CONTRATADO'
  const acoesStatus = detalhe?.status === 'CONTRATADO' ? ['COLETADO'] : detalhe?.status === 'COLETADO' ? ['EM_TRANSITO', 'ENTREGUE'] : detalhe?.status === 'EM_TRANSITO' ? ['ENTREGUE'] : []
  const totalPaginas = Math.max(1, Math.ceil(total / 20))

  if (idAberto) return <section className="mx-auto max-w-6xl">
    <div className="mb-5 flex flex-wrap items-center justify-between gap-3"><button className="text-sm font-medium text-blue-800 underline" onClick={() => { setIdAberto(null); setDetalhe(null); setFormAberto(false); void carregarLista() }}>← Voltar aos fretes</button>{detalhe && <span className="text-sm text-slate-600">{detalhe.codigo} · {statusLabels[detalhe.status] ?? detalhe.status}</span>}</div>
    <Mensagem texto={erro} /><Mensagem texto={sucesso} sucesso />
    {carregandoDetalhe || !detalhe ? <p className="py-10 text-sm text-slate-600">Carregando frete…</p> : <>
      <Cabecalho titulo={`${detalhe.codigo} · ${statusLabels[detalhe.status] ?? detalhe.status}`} descricao={`${detalhe.dados.origemCidade}/${detalhe.dados.origemUf} → ${detalhe.dados.destinoCidade}/${detalhe.dados.destinoUf} · ${detalhe.dados.destinatarioNome}`} acao={podeCotacao ? <button className={secondary} onClick={() => editar(detalhe)}>Editar dados</button> : undefined} />
      {formAberto && <form onSubmit={salvarFrete} className={`${panel} mt-5 p-5`}><h2 className="text-lg font-semibold">Editar frete</h2><FreteCampos value={formulario} onChange={setFormulario} /><div className="mt-5 flex gap-3"><button disabled={salvando} className={primary}>Salvar</button><button type="button" className={secondary} onClick={() => setFormAberto(false)}>Cancelar</button></div></form>}
      <div className={`${panel} mt-6 p-5`}><div className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-lg font-semibold">Cotações</h2>{podeCotacao && <button className={primary} onClick={() => void agir(() => simularCotacao(token, detalhe.id), 'Cotação por tabela atualizada.')}>Calcular pela tabela</button>}</div>
        {podeCotacao && <form onSubmit={adicionarProposta} className="mt-5 grid gap-3 border-t border-slate-200 pt-5 sm:grid-cols-2 lg:grid-cols-5"><div className="lg:col-span-2"><label className={label} htmlFor="parceiro-proposta">Transportadora</label><select id="parceiro-proposta" className={input} required value={propostaParceiro} onChange={e => setPropostaParceiro(e.target.value)}><option value="">Selecione</option>{parceiros.map(x => <option key={x.id} value={x.id}>{x.nome}</option>)}</select></div><div><label className={label} htmlFor="valor-proposta">Valor (R$)</label><input id="valor-proposta" className={input} type="number" min="0.01" step="0.01" required value={propostaValor || ''} onChange={e => setPropostaValor(Number(e.target.value))} /></div><div><label className={label} htmlFor="prazo-proposta">Prazo (dias)</label><input id="prazo-proposta" className={input} type="number" min="1" required value={propostaPrazo} onChange={e => setPropostaPrazo(Number(e.target.value))} /></div><div className="flex items-end"><button className={primary} disabled={!propostaParceiro}>Registrar proposta</button></div><div className="sm:col-span-2 lg:col-span-5"><label className={label} htmlFor="obs-proposta">Observação (opcional)</label><input id="obs-proposta" className={input} maxLength={500} value={propostaObservacao} onChange={e => setPropostaObservacao(e.target.value)} /></div></form>}
        {detalhe.propostas.length === 0 ? <p className="py-6 text-sm text-slate-600">Nenhuma proposta registrada para este frete.</p> : <div className="mt-4 overflow-x-auto"><table className="min-w-full text-left text-sm"><thead className="bg-slate-50 text-xs uppercase text-slate-600"><tr>{['Transportadora', 'Origem', 'Valor', 'Prazo', 'Destaques', 'Ação'].map(x => <th key={x} className="px-4 py-3">{x}</th>)}</tr></thead><tbody className="divide-y divide-slate-200">{detalhe.propostas.map(x => <tr key={x.id} className={x.selecionada ? 'bg-blue-50' : ''}><td className="px-4 py-3 font-medium">{x.transportadora}</td><td className="px-4 py-3">{x.origem === 'TABELA' ? 'Tabela' : 'Manual'}</td><td className="whitespace-nowrap px-4 py-3">{dinheiro(x.valor)}</td><td className="px-4 py-3">{x.prazoDias} dias</td><td className="px-4 py-3">{x.id === maisBarata && <span className="mr-2">Menor valor</span>}{x.id === maisRapida && <span>Menor prazo</span>}{x.selecionada && <strong> · Escolhida</strong>}</td><td className="whitespace-nowrap px-4 py-3">{podeEscolher && <button className="mr-3 text-blue-800 underline" onClick={() => void agir(() => selecionarProposta(token, detalhe.id, x.id), 'Proposta selecionada.')}>Escolher</button>}{podeCotacao && <button className="text-red-700 underline" onClick={() => void agir(() => removerProposta(token, detalhe.id, x.id), 'Proposta removida.')}>Remover</button>}</td></tr>)}</tbody></table></div>}
      </div>
      <div className={`${panel} mt-5 p-5`}><div className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-lg font-semibold">Acompanhamento</h2><div className="flex flex-wrap gap-2">{acoesStatus.map(x => <button key={x} className={primary} onClick={() => void agir(() => alterarStatusFrete(token, detalhe.id, x), `Status atualizado: ${statusLabels[x]}.`)}>{statusLabels[x]}</button>)}{['ABERTO', 'EM_COTACAO', 'CONTRATADO'].includes(detalhe.status) && <button className={secondary} onClick={() => void cancelarFrete()}>Cancelar frete</button>}</div></div>
        <div className="mt-4 overflow-x-auto"><table className="min-w-full text-left text-sm"><thead className="bg-slate-50 text-xs uppercase text-slate-600"><tr><th className="px-4 py-3">Data e hora</th><th className="px-4 py-3">Status anterior</th><th className="px-4 py-3">Novo status</th><th className="px-4 py-3">Observação</th></tr></thead><tbody className="divide-y divide-slate-200">{detalhe.historico.map((x, i) => <tr key={`${x.statusNovo}-${i}`}><td className="px-4 py-3">{new Date(x.ocorridoEm).toLocaleString('pt-BR')}</td><td className="px-4 py-3">{x.statusAnterior ? statusLabels[x.statusAnterior] : '—'}</td><td className="px-4 py-3">{statusLabels[x.statusNovo] ?? x.statusNovo}</td><td className="px-4 py-3">{x.observacao || '—'}</td></tr>)}</tbody></table></div>
      </div>
      <Documentos detalhe={detalhe} tipo={docTipo} setTipo={setDocTipo} numero={docNumero} setNumero={setDocNumero} chave={docChave} setChave={setDocChave} valor={docValor} setValor={setDocValor} aoAdicionar={adicionarDocumento} />
    </>}
  </section>

  return <section className="mx-auto max-w-6xl">
    <Cabecalho titulo="Fretes" descricao="Cadastre envios, compare cotações e acompanhe cada etapa." acao={<button className={primary} onClick={novo}>Novo frete</button>} />
    <Mensagem texto={erro} /><Mensagem texto={sucesso} sucesso />
    {formAberto && <form onSubmit={salvarFrete} className={`${panel} mt-6 p-5`}><h2 className="text-lg font-semibold">{idEdicao ? 'Editar frete' : 'Novo frete'}</h2><FreteCampos value={formulario} onChange={setFormulario} /><div className="mt-5 flex gap-3"><button disabled={salvando} className={primary}>{salvando ? 'Salvando…' : 'Salvar frete'}</button><button type="button" className={secondary} onClick={() => setFormAberto(false)}>Cancelar</button></div></form>}
    <form className={`${panel} mt-6 grid gap-3 p-5 sm:grid-cols-2 lg:grid-cols-4`} onSubmit={e => { e.preventDefault(); void carregarLista(1) }}><div className="lg:col-span-2"><label className={label} htmlFor="busca-frete">Buscar</label><input id="busca-frete" className={input} placeholder="Código, cidade ou destinatário" value={busca} onChange={e => setBusca(e.target.value)} /></div><div><label className={label} htmlFor="status-filtro">Status</label><select id="status-filtro" className={input} value={statusFiltro} onChange={e => setStatusFiltro(e.target.value)}><option value="">Todos</option>{Object.entries(statusLabels).map(([key, texto]) => <option key={key} value={key}>{texto}</option>)}</select></div><div className="flex items-end gap-3"><label className="flex items-center gap-2 pb-2 text-sm"><input type="checkbox" checked={somenteAtrasados} onChange={e => setSomenteAtrasados(e.target.checked)} />Atrasados</label><button className={secondary}>Filtrar</button></div></form>
    <div className={`${panel} mt-5 overflow-hidden`}><div className="border-b border-slate-200 px-5 py-4"><h2 className="font-semibold">Fretes da empresa ({total})</h2></div>{carregando ? <p className="px-5 py-8 text-sm text-slate-600">Carregando fretes…</p> : itens.length === 0 ? <p className="px-5 py-8 text-sm text-slate-600">Nenhum frete encontrado.</p> : <div className="overflow-x-auto"><table className="min-w-full text-left text-sm"><thead className="bg-slate-50 text-xs uppercase text-slate-600"><tr>{['Código', 'Rota', 'Destinatário', 'Status', 'Valor', 'Prazo', ''].map(x => <th key={x} className="px-4 py-3">{x}</th>)}</tr></thead><tbody className="divide-y divide-slate-200">{itens.map(x => <tr key={x.id}><td className="whitespace-nowrap px-4 py-3 font-medium">{x.codigo}</td><td className="whitespace-nowrap px-4 py-3">{x.origemCidade}/{x.origemUf} → {x.destinoCidade}/{x.destinoUf}</td><td className="px-4 py-3">{x.destinatarioNome}</td><td className="px-4 py-3">{statusLabels[x.status] ?? x.status}{x.atrasado && <span className="ml-2 font-semibold text-red-700">Atrasado</span>}</td><td className="px-4 py-3">{dinheiro(x.valorContratado)}</td><td className="px-4 py-3">{x.prazoPrevisto || '—'}</td><td className="px-4 py-3"><button className="text-blue-800 underline" onClick={() => setIdAberto(x.id)}>Abrir</button></td></tr>)}</tbody></table></div>}
      <div className="flex items-center justify-between border-t border-slate-200 px-5 py-3 text-sm"><span>Página {pagina} de {totalPaginas}</span><div className="flex gap-2"><button className={secondary} disabled={pagina <= 1 || carregando} onClick={() => void carregarLista(pagina - 1)}>Anterior</button><button className={secondary} disabled={pagina >= totalPaginas || carregando} onClick={() => void carregarLista(pagina + 1)}>Próxima</button></div></div>
    </div>
  </section>
}

function FreteCampos({ value, onChange }: { value: FreteEntrada; onChange: (x: FreteEntrada) => void }) {
  const set = (chave: keyof DadosFrete, valor: string | number | null) => onChange({ ...value, [chave]: valor })
  return <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
    <div><label className={label} htmlFor="origem-cep">CEP de origem</label><input className={input} id="origem-cep" inputMode="numeric" maxLength={8} required value={value.origemCep} onChange={e => set('origemCep', somenteDigitos(e.target.value))} /></div>
    <div><label className={label} htmlFor="origem-cidade">Cidade de origem</label><input className={input} id="origem-cidade" required value={value.origemCidade} onChange={e => set('origemCidade', e.target.value)} /></div>
    <div><label className={label} htmlFor="origem-uf">UF de origem</label><input className={input} id="origem-uf" maxLength={2} required value={value.origemUf} onChange={e => set('origemUf', e.target.value.toUpperCase())} /></div>
    <div><label className={label} htmlFor="destino-cep">CEP de destino</label><input className={input} id="destino-cep" inputMode="numeric" maxLength={8} required value={value.destinoCep} onChange={e => set('destinoCep', somenteDigitos(e.target.value))} /></div>
    <div><label className={label} htmlFor="destino-cidade">Cidade de destino</label><input className={input} id="destino-cidade" required value={value.destinoCidade} onChange={e => set('destinoCidade', e.target.value)} /></div>
    <div><label className={label} htmlFor="destino-uf">UF de destino</label><input className={input} id="destino-uf" maxLength={2} required value={value.destinoUf} onChange={e => set('destinoUf', e.target.value.toUpperCase())} /></div>
    <div><label className={label} htmlFor="destinatario">Destinatário</label><input className={input} id="destinatario" required value={value.destinatarioNome} onChange={e => set('destinatarioNome', e.target.value)} /></div>
    <div><label className={label} htmlFor="peso">Peso (kg)</label><input className={input} id="peso" type="number" min="0.01" step="0.01" required value={value.pesoKg || ''} onChange={e => set('pesoKg', Number(e.target.value))} /></div>
    <div><label className={label} htmlFor="volume">Volume (m³)</label><input className={input} id="volume" type="number" min="0" step="0.001" value={value.volumeM3 || ''} onChange={e => set('volumeM3', Number(e.target.value))} /></div>
    <div><label className={label} htmlFor="valor-mercadoria">Valor da mercadoria (R$)</label><input className={input} id="valor-mercadoria" type="number" min="0.01" step="0.01" value={value.valorMercadoria ?? ''} onChange={e => set('valorMercadoria', e.target.value ? Number(e.target.value) : null)} /></div>
    <div className="sm:col-span-2 lg:col-span-3"><label className={label} htmlFor="observacao-frete">Observação</label><textarea className={input} id="observacao-frete" rows={2} maxLength={1000} value={value.observacao ?? ''} onChange={e => set('observacao', e.target.value)} /></div>
  </div>
}

function Documentos({ detalhe, tipo, setTipo, numero, setNumero, chave, setChave, valor, setValor, aoAdicionar }: {
  detalhe: FreteDetalhe; tipo: 'NFE' | 'CTE' | 'OUTRO'; setTipo: (x: 'NFE' | 'CTE' | 'OUTRO') => void
  numero: string; setNumero: (x: string) => void; chave: string; setChave: (x: string) => void
  valor: string; setValor: (x: string) => void; aoAdicionar: (e: FormEvent) => void
}) {
  return <div className={`${panel} mt-5 p-5`}><h2 className="text-lg font-semibold">Documentos fiscais</h2><p className="mt-1 text-sm text-slate-600">Registre os metadados. O arquivo da nota não é enviado.</p>
    <form onSubmit={aoAdicionar} className="mt-4 grid gap-3 border-t border-slate-200 pt-4 sm:grid-cols-2 lg:grid-cols-4"><div><label className={label} htmlFor="tipo-doc">Tipo</label><select className={input} id="tipo-doc" value={tipo} onChange={e => setTipo(e.target.value as 'NFE' | 'CTE' | 'OUTRO')}><option value="NFE">NF-e</option><option value="CTE">CT-e</option><option value="OUTRO">Outro</option></select></div><div><label className={label} htmlFor="numero-doc">Número</label><input className={input} id="numero-doc" required maxLength={60} value={numero} onChange={e => setNumero(e.target.value)} /></div>{tipo === 'NFE' && <div className="lg:col-span-2"><label className={label} htmlFor="chave-doc">Chave de acesso (44 números)</label><input className={input} id="chave-doc" inputMode="numeric" minLength={44} maxLength={44} pattern="[0-9]{44}" required value={chave} onChange={e => setChave(e.target.value.replace(/\D/g, '').slice(0, 44))} /></div>}<div><label className={label} htmlFor="valor-doc">Valor (opcional)</label><input className={input} id="valor-doc" type="number" min="0.01" step="0.01" value={valor} onChange={e => setValor(e.target.value)} /></div><div className="flex items-end"><button className={primary}>Registrar documento</button></div></form>
    {detalhe.documentos.length === 0 ? <p className="mt-5 text-sm text-slate-600">Nenhum documento registrado.</p> : <div className="mt-5 overflow-x-auto"><table className="min-w-full text-left text-sm"><thead className="bg-slate-50 text-xs uppercase text-slate-600"><tr><th className="px-4 py-3">Tipo</th><th className="px-4 py-3">Número</th><th className="px-4 py-3">Chave</th><th className="px-4 py-3">Valor</th></tr></thead><tbody className="divide-y divide-slate-200">{detalhe.documentos.map((x: DocumentoFiscal) => <tr key={x.id}><td className="px-4 py-3">{x.tipo}</td><td className="px-4 py-3">{x.numero}</td><td className="px-4 py-3">{x.chaveAcesso || '—'}</td><td className="px-4 py-3">{dinheiro(x.valor)}</td></tr>)}</tbody></table></div>}
  </div>
}
