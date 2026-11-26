import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import FavoriteButton from '../components/ui/FavoriteButton'

let mockFavorite = { isFavorite: false, toggle: vi.fn(), loading: false }
vi.mock('../hooks/useFavorite', () => ({
  default: () => mockFavorite,
}))

const hoverButton = () => fireEvent.mouseOver(screen.getByRole('button'))

describe('FavoriteButton', () => {
  it('shows the add tooltip on hover when the game is not a favorite', async () => {
    mockFavorite = { isFavorite: false, toggle: vi.fn(), loading: false }
    render(<FavoriteButton game={{ id: 1, name: 'Wordly' }} />)

    hoverButton()

    expect(await screen.findByRole('tooltip')).toHaveTextContent('Add to favorites')
  })

  it('shows the remove tooltip on hover when the game is a favorite', async () => {
    mockFavorite = { isFavorite: true, toggle: vi.fn(), loading: false }
    render(<FavoriteButton game={{ id: 1, name: 'Wordly' }} />)

    hoverButton()

    expect(await screen.findByRole('tooltip')).toHaveTextContent('Remove from favorites')
  })
})
