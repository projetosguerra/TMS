import type { ReactNode } from 'react'

export const input = 'mt-1 block w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:border-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-200'
export const label = 'block text-sm font-medium text-slate-700'
export const primary = 'rounded-md bg-blue-800 px-4 py-2.5 text-sm font-semibold text-white hover:bg-blue-900 disabled:cursor-wait disabled:opacity-60'
export const secondary = 'rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50'
export const panel = 'rounded-lg border border-slate-200 bg-white shadow-sm'
export function Mensagem({ texto, sucesso = false }: { texto: string; sucesso?: boolean }) {
  if (!texto) return null
  return <div role={sucesso ? 'status' : 'alert'} className={`mt-5 rounded-md border px-4 py-3 text-sm ${sucesso ? 'border-green-200 bg-green-50 text-green-800' : 'border-red-200 bg-red-50 text-red-800'}`}>{texto}</div>
}
export function Cabecalho({ titulo, descricao, acao }: { titulo: string; descricao?: string; acao?: ReactNode }) {
  return <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end"><div><h1 className="text-2xl font-semibold">{titulo}</h1>{descricao && <p className="mt-1 text-sm text-slate-600">{descricao}</p>}</div>{acao}</div>
}
