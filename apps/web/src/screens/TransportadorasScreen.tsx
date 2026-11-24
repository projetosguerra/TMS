import { useEffect, useState, type FormEvent } from 'react'
import { alterarSituacao, atualizarTransportadora, criarTransportadora, listarTransportadoras, type Transportadora, type TransportadoraEntrada } from '../services/api'
import { Cabecalho, input, label, Mensagem, panel, primary, secondary } from './ui'

const vazio: TransportadoraEntrada = { tipo: 'TRANSPORTADORA', nome: '', documento: '', telefone: '', email: '' }

export function TransportadorasScreen({ token, abrirTabelas }: { token: string; abrirTabelas: (id: string) => void }) {
  const [itens, setItens] = useState<Transportadora[]>([])
  const [formulario, setFormulario] = useState<TransportadoraEntrada>(vazio)
  const [idEdicao, setIdEdicao] = useState<string | null>(null)
  const [aberto, setAberto] = useState(false)
  const [carregando, setCarregando] = useState(false)
  const [salvando, setSalvando] = useState(false)
  const [erro, setErro] = useState('')
  const [sucesso, setSucesso] = useState('')

  async function carregar() {
    setCarregando(true)
    try { setItens(await listarTransportadoras(token)); setErro('') }
    catch (e) { setErro(e instanceof Error ? e.message : 'Não foi possível carregar os cadastros.') }
    finally { setCarregando(false) }
  }
  useEffect(() => { void carregar() }, [token])

  function novo() { setIdEdicao(null); setFormulario(vazio); setAberto(true); setErro(''); setSucesso('') }
  function editar(item: Transportadora) {
    setIdEdicao(item.id)
    setFormulario({ tipo: item.tipo, nome: item.nome, documento: item.documento, telefone: item.telefone ?? '', email: item.email ?? '' })
    setAberto(true); setErro(''); setSucesso('')
  }
  async function salvar(evento: FormEvent) {
    evento.preventDefault(); setSalvando(true); setErro(''); setSucesso('')
    const entrada = { ...formulario, nome: formulario.nome.trim(), documento: formulario.documento.replace(/\D/g, ''), telefone: formulario.telefone?.trim() || null, email: formulario.email?.trim() || null }
    try {
      if (idEdicao) { await atualizarTransportadora(token, idEdicao, entrada); setSucesso('Cadastro atualizado.') }
      else { await criarTransportadora(token, entrada); setSucesso('Cadastro criado.') }
      setAberto(false); await carregar()
    } catch (e) { setErro(e instanceof Error ? e.message : 'Não foi possível salvar o cadastro.') }
    finally { setSalvando(false) }
  }
  async function desativar(item: Transportadora) {
    if (!window.confirm(`Desativar ${item.nome}? O cadastro será preservado.`)) return
    try { await alterarSituacao(token, item.id, false); setSucesso('Cadastro desativado.'); await carregar() }
    catch (e) { setErro(e instanceof Error ? e.message : 'Não foi possível desativar o cadastro.') }
  }

  return <section className="mx-auto max-w-6xl">
    <Cabecalho titulo="Transportadoras" descricao="Cadastre transportadoras e motoristas autônomos da sua empresa." acao={<button className={primary} onClick={novo}>Cadastrar transportadora</button>} />
    <Mensagem texto={erro} /><Mensagem texto={sucesso} sucesso />
    {aberto && <form onSubmit={salvar} className={`${panel} mt-6 p-5`}>
      <h2 className="text-lg font-semibold">{idEdicao ? 'Editar cadastro' : 'Novo cadastro'}</h2>
      <div className="mt-4 grid gap-4 sm:grid-cols-2">
        <div><label className={label} htmlFor="tipo-parceiro">Tipo</label><select id="tipo-parceiro" className={input} value={formulario.tipo} onChange={e => setFormulario({ ...formulario, tipo: e.target.value as TransportadoraEntrada['tipo'], documento: '' })}><option value="TRANSPORTADORA">Transportadora (CNPJ)</option><option value="AUTONOMO">Motorista autônomo (CPF)</option></select></div>
        <div><label className={label} htmlFor="nome-parceiro">Nome</label><input id="nome-parceiro" className={input} maxLength={150} required value={formulario.nome} onChange={e => setFormulario({ ...formulario, nome: e.target.value })} /></div>
        <div><label className={label} htmlFor="documento-parceiro">{formulario.tipo === 'AUTONOMO' ? 'CPF' : 'CNPJ'}</label><input id="documento-parceiro" className={input} inputMode="numeric" maxLength={formulario.tipo === 'AUTONOMO' ? 11 : 14} required value={formulario.documento} onChange={e => setFormulario({ ...formulario, documento: e.target.value.replace(/\D/g, '').slice(0, formulario.tipo === 'AUTONOMO' ? 11 : 14) })} /><p className="mt-1 text-xs text-slate-500">Digite somente os números.</p></div>
        <div><label className={label} htmlFor="telefone-parceiro">Telefone (opcional)</label><input id="telefone-parceiro" className={input} type="tel" maxLength={30} value={formulario.telefone ?? ''} onChange={e => setFormulario({ ...formulario, telefone: e.target.value })} /></div>
        <div className="sm:col-span-2"><label className={label} htmlFor="email-parceiro">E-mail de contato (opcional)</label><input id="email-parceiro" className={input} type="email" maxLength={254} value={formulario.email ?? ''} onChange={e => setFormulario({ ...formulario, email: e.target.value })} /></div>
      </div>
      <div className="mt-5 flex gap-3"><button className={primary} disabled={salvando}>{salvando ? 'Salvando…' : 'Salvar'}</button><button type="button" className={secondary} onClick={() => setAberto(false)}>Cancelar</button></div>
    </form>}
    <div className={`${panel} mt-6 overflow-hidden`}>
      <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4"><h2 className="font-semibold">Cadastros da empresa</h2><button className="text-sm font-medium text-blue-800 underline" disabled={carregando} onClick={() => void carregar()}>{carregando ? 'Atualizando…' : 'Atualizar lista'}</button></div>
      {carregando && itens.length === 0 ? <p className="px-5 py-8 text-sm text-slate-600">Carregando…</p> : itens.length === 0 ? <p className="px-5 py-8 text-sm text-slate-600">Nenhum cadastro encontrado.</p> : <div className="overflow-x-auto"><table className="min-w-full text-left text-sm"><thead className="bg-slate-50 text-xs uppercase text-slate-600"><tr>{['Nome', 'Tipo', 'Documento', 'Contato', 'Situação', 'Ações'].map(x => <th key={x} className="px-5 py-3">{x}</th>)}</tr></thead><tbody className="divide-y divide-slate-200">{itens.map(item => <tr key={item.id}>
        <td className="whitespace-nowrap px-5 py-4 font-medium">{item.nome}</td><td className="whitespace-nowrap px-5 py-4">{item.tipo === 'AUTONOMO' ? 'Motorista autônomo' : 'Transportadora'}</td><td className="whitespace-nowrap px-5 py-4">{item.documento}</td><td className="px-5 py-4">{item.telefone || item.email || '—'}{item.telefone && item.email && <div className="text-slate-500">{item.email}</div>}</td><td className="px-5 py-4">{item.ativo ? 'Ativo' : 'Inativo'}</td>
        <td className="whitespace-nowrap px-5 py-4"><div className="flex gap-3"><button className="text-blue-800 underline" onClick={() => editar(item)}>Editar</button><button className="text-blue-800 underline" onClick={() => abrirTabelas(item.id)}>Tabelas de frete</button>{item.ativo && <button className="text-red-700 underline" onClick={() => void desativar(item)}>Desativar</button>}</div></td>
      </tr>)}</tbody></table></div>}
    </div>
  </section>
}
