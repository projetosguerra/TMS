export type TipoParceiro = 'TRANSPORTADORA' | 'AUTONOMO'

export type Transportadora = {
  id: string
  empresaId: string
  tipo: TipoParceiro
  nome: string
  documento: string
  telefone: string | null
  email: string | null
  ativo: boolean
  criadoEm: string
}

export type UsuarioDaSessao = {
  id: string
  nome: string
  perfil: string
  empresa: { id: string; nome: string }
}

export type TransportadoraEntrada = Omit<Transportadora, 'id' | 'empresaId' | 'ativo' | 'criadoEm'>

export type TabelaFrete = {
  id: string
  transportadoraId: string
  ufOrigem: string
  ufDestino: string
  valorMinimo: number
  valorPorKg: number
  prazoDias: number
  ativa: boolean
}

export type TabelaFreteEntrada = Omit<TabelaFrete, 'id' | 'transportadoraId'>
export type DadosFrete = {
  origemCep: string; origemCidade: string; origemUf: string
  destinoCep: string; destinoCidade: string; destinoUf: string
  destinatarioNome: string; pesoKg: number; volumeM3: number
  valorMercadoria: number | null; observacao: string | null
}
export type FreteEntrada = DadosFrete
export type FreteListaItem = {
  id: string; codigo: string; status: string
  origemCidade: string; origemUf: string; destinoCidade: string; destinoUf: string
  destinatarioNome: string; transportadoraId: string | null; valorContratado: number | null
  prazoPrevisto: string | null; atrasado: boolean; criadoEm: string
}
export type Proposta = {
  id: string; transportadoraId: string; transportadora: string; origem: 'TABELA' | 'MANUAL'
  valor: number; prazoDias: number; observacao: string | null; selecionada: boolean
}
export type HistoricoStatus = {
  statusAnterior: string | null; statusNovo: string; observacao: string | null
  usuarioId: string; ocorridoEm: string
}
export type DocumentoFiscal = {
  id: string; freteId: string; tipo: 'NFE' | 'CTE' | 'OUTRO'; numero: string
  chaveAcesso: string | null; valor: number | null; criadoEm: string
}
export type FreteDetalhe = {
  id: string; codigo: string; status: string; dados: DadosFrete; criadoPor: string
  transportadoraId: string | null; propostaSelecionadaId: string | null
  valorContratado: number | null; prazoPrevisto: string | null; entregueEm: string | null
  criadoEm: string; atualizadoEm: string; propostas: Proposta[]
  historico: HistoricoStatus[]; documentos: DocumentoFiscal[]
}
export type FreteResumo = Pick<FreteDetalhe, 'id' | 'codigo' | 'status' | 'dados' | 'transportadoraId' | 'valorContratado' | 'prazoPrevisto' | 'criadoEm'>
export type Pagina<T> = { itens: T[]; pagina: number; tamanho: number; total: number }
export type Dashboard = {
  fretesAtivos: number; entreguesNoPeriodo: number; percentualNoPrazo: number
  custoTotal: number; custoMedioPorFrete: number; tempoMedioEntregaDias: number
  fretesAtrasados: number; rankingTransportadoras: {
    transportadoraId: string; transportadora: string; quantidade: number
    custoMedio: number; percentualNoPrazo: number
  }[]; de: string; ate: string
}

const urlSupabase = import.meta.env.VITE_SUPABASE_URL?.replace(/\/$/, '')
const chavePublicavel = import.meta.env.VITE_SUPABASE_PUBLISHABLE_KEY || import.meta.env.VITE_SUPABASE_ANON_KEY
const urlApi = (import.meta.env.VITE_API_URL || 'http://localhost:5000').replace(/\/$/, '')

async function mensagemDeErro(resposta: Response): Promise<string> {
  const problema = await resposta.json().catch(() => null) as {
    title?: string
    errors?: Record<string, string[]>
  } | null
  const campos = problema?.errors ? Object.values(problema.errors).flat() : []
  if (campos.length > 0) return campos.join(' ')
  if (resposta.status === 401) return 'Sua sessão expirou. Entre novamente.'
  if (resposta.status === 403) return 'Seu usuário não tem acesso a este cadastro.'
  if (resposta.status === 409) return problema?.title || 'Já existe um cadastro com este documento.'
  return problema?.title || 'Não foi possível concluir a solicitação.'
}

export async function entrar(email: string, senha: string): Promise<string> {
  if (!urlSupabase || !chavePublicavel) {
    throw new Error('Configure VITE_SUPABASE_URL e VITE_SUPABASE_PUBLISHABLE_KEY no arquivo local apps/web/.env.local.')
  }
  const resposta = await fetch(`${urlSupabase}/auth/v1/token?grant_type=password`, {
    method: 'POST',
    headers: { apikey: chavePublicavel, 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: senha }),
  })
  if (!resposta.ok) throw new Error('E-mail ou senha incorretos. Confira os dados ou use a recuperação de senha.')
  const sessao = await resposta.json() as { access_token?: string }
  if (!sessao.access_token) throw new Error('O Supabase Auth não retornou um token de acesso.')
  return sessao.access_token
}

async function requisitar<T>(caminho: string, token: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  headers.set('Authorization', `Bearer ${token}`)
  if (init.body) headers.set('Content-Type', 'application/json')
  const resposta = await fetch(`${urlApi}${caminho}`, {
    ...init,
    headers,
  })
  if (!resposta.ok) throw new Error(await mensagemDeErro(resposta))
  if (resposta.status === 204) return undefined as T
  return await resposta.json() as T
}

export const obterUsuario = (token: string) => requisitar<UsuarioDaSessao>('/api/me', token)
export const listarTransportadoras = (token: string) => requisitar<Transportadora[]>('/api/transportadoras', token)
export const criarTransportadora = (token: string, entrada: TransportadoraEntrada) =>
  requisitar<Transportadora>('/api/transportadoras', token, { method: 'POST', body: JSON.stringify(entrada) })
export const atualizarTransportadora = (token: string, id: string, entrada: TransportadoraEntrada) =>
  requisitar<Transportadora>(`/api/transportadoras/${id}`, token, { method: 'PUT', body: JSON.stringify(entrada) })
export const alterarSituacao = (token: string, id: string, ativo: boolean) =>
  requisitar<Transportadora>(`/api/transportadoras/${id}/ativo`, token, {
    method: 'PATCH', body: JSON.stringify({ ativo }),
  })

export const listarTabelas = (token: string, id: string) =>
  requisitar<TabelaFrete[]>(`/api/transportadoras/${id}/tabelas`, token)
export const criarTabela = (token: string, id: string, entrada: TabelaFreteEntrada) =>
  requisitar<TabelaFrete>(`/api/transportadoras/${id}/tabelas`, token, { method: 'POST', body: JSON.stringify(entrada) })
export const atualizarTabela = (token: string, id: string, entrada: TabelaFreteEntrada) =>
  requisitar<TabelaFrete>(`/api/tabelas-frete/${id}`, token, { method: 'PUT', body: JSON.stringify(entrada) })

export function listarFretes(token: string, filtros: { status?: string; busca?: string; atrasados?: boolean; pagina?: number; tamanho?: number } = {}) {
  const query = new URLSearchParams()
  if (filtros.status) query.set('status', filtros.status)
  if (filtros.busca) query.set('busca', filtros.busca)
  if (filtros.atrasados) query.set('atrasados', 'true')
  if (filtros.pagina) query.set('pagina', String(filtros.pagina))
  if (filtros.tamanho) query.set('tamanho', String(filtros.tamanho))
  return requisitar<Pagina<FreteListaItem>>(`/api/fretes?${query}`, token)
}
export const obterFrete = (token: string, id: string) => requisitar<FreteDetalhe>(`/api/fretes/${id}`, token)
export const criarFrete = (token: string, entrada: FreteEntrada) =>
  requisitar<FreteResumo>(`/api/fretes`, token, { method: 'POST', body: JSON.stringify(entrada) })
export const atualizarFrete = (token: string, id: string, entrada: FreteEntrada) =>
  requisitar<FreteResumo>(`/api/fretes/${id}`, token, { method: 'PUT', body: JSON.stringify(entrada) })
export const simularCotacao = (token: string, id: string) =>
  requisitar<unknown[]>(`/api/fretes/${id}/cotacao/simular`, token, { method: 'POST' })
export const registrarProposta = (token: string, id: string, entrada: { transportadoraId: string; valor: number; prazoDias: number; observacao?: string }) =>
  requisitar<Proposta>(`/api/fretes/${id}/propostas`, token, { method: 'POST', body: JSON.stringify(entrada) })
export const removerProposta = (token: string, id: string, propostaId: string) =>
  requisitar<void>(`/api/fretes/${id}/propostas/${propostaId}`, token, { method: 'DELETE' })
export const selecionarProposta = (token: string, id: string, propostaId: string) =>
  requisitar<FreteResumo>(`/api/fretes/${id}/propostas/${propostaId}/selecionar`, token, { method: 'POST' })
export const alterarStatusFrete = (token: string, id: string, novoStatus: string, observacao?: string) =>
  requisitar<FreteResumo>(`/api/fretes/${id}/status`, token, { method: 'POST', body: JSON.stringify({ novoStatus, observacao }) })
export const criarDocumentoFiscal = (token: string, id: string, entrada: { tipo: string; numero: string; chaveAcesso?: string; valor?: number }) =>
  requisitar<DocumentoFiscal>(`/api/fretes/${id}/documentos`, token, { method: 'POST', body: JSON.stringify(entrada) })
export const obterDashboard = (token: string, de: string, ate: string) =>
  requisitar<Dashboard>(`/api/dashboard?de=${encodeURIComponent(de)}&ate=${encodeURIComponent(ate)}`, token)
