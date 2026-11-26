import api from './index'

const toGameList = data => (Array.isArray(data) ? data : [])

/**
 * All games, with the caller's favourite/submitted flags. Resolves to a list
 * (empty when the response is not one); rejects when the request fails.
 *
 * @returns {Promise<object[]>}
 */
export function getGames() {
  return api.get('/games').then(response => toGameList(response.data))
}
