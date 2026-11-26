/**
 * Makes identical GETs in flight on `client` share one network request
 * (React.StrictMode double-mounts, several components asking for the same
 * resource). A request opts out with `{ dedupe: false }`.
 */
export default function dedupeIdenticalGets(client) {
  const inflightGets = new Map()
  const sendGet = client.get.bind(client)

  const inflightKey = (url, config) => {
    const base = client.defaults.baseURL || ''
    const params = config && config.params ? JSON.stringify(config.params) : ''
    return `GET:${base}${url}?params=${params}`
  }

  client.get = (url, config = {}) => {
    const isDedupeEnabled = config.dedupe !== false
    if (!isDedupeEnabled) return sendGet(url, config)

    const key = inflightKey(url, config)
    if (inflightGets.has(key)) return inflightGets.get(key)

    const request = sendGet(url, config).finally(() => {
      inflightGets.delete(key)
    })
    inflightGets.set(key, request)
    return request
  }
}
