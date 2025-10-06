import axios, { type AxiosInstance, type AxiosRequestHeaders } from 'axios'

const identityBaseUrl = import.meta.env.VITE_IDENTITY_API_BASE_URL ?? 'http://localhost:5123'
const cineReviewBaseUrl = import.meta.env.VITE_CINEREVIEW_API_BASE_URL ?? 'http://localhost:5216'

type TokenProvider = () => string | null

type ConfigureOptions = {
  getToken: TokenProvider
  onUnauthorized?: () => void
}

let tokenProvider: TokenProvider = () => null
let unauthorizedHandler: (() => void) | undefined

const defaultHeaders = {
  'Content-Type': 'application/json',
}

export const identityClient = axios.create({
  baseURL: identityBaseUrl,
  headers: defaultHeaders,
})

export const cineReviewClient = axios.create({
  baseURL: cineReviewBaseUrl,
  headers: defaultHeaders,
})

const clients: AxiosInstance[] = [identityClient, cineReviewClient]

function attachInterceptors(instance: AxiosInstance) {
  instance.interceptors.request.use((config) => {
    const token = tokenProvider()
    if (token) {
      if (config.headers && typeof (config.headers as any).set === 'function') {
        ;(config.headers as any).set(
          'Authorization',
          (config.headers as any).get?.('Authorization') ?? `Bearer ${token}`,
        )
      } else {
        config.headers = {
          ...(config.headers as AxiosRequestHeaders | undefined),
          Authorization:
            (config.headers as AxiosRequestHeaders | undefined)?.Authorization ??
            `Bearer ${token}`,
        } as AxiosRequestHeaders
      }
    }

    return config
  })

  instance.interceptors.response.use(
    (response) => response,
    (error) => {
      if (error?.response?.status === 401) {
        unauthorizedHandler?.()
      }

      return Promise.reject(error)
    },
  )
}

clients.forEach(attachInterceptors)

export function configureApiClients({ getToken, onUnauthorized }: ConfigureOptions) {
  tokenProvider = getToken
  unauthorizedHandler = onUnauthorized
}

export function updateApiBaseUrls(urls: { identity: string; cineReview: string }) {
  identityClient.defaults.baseURL = urls.identity
  cineReviewClient.defaults.baseURL = urls.cineReview
}
