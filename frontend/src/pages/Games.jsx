import React, { useEffect, useState } from 'react'
import { Card, CardContent, CardMedia, Typography, Grid, Button, CardActionArea } from '@mui/material'
import { Link } from 'react-router-dom'
import api from '../api'

export default function Games() {
  const [games, setGames] = useState([])

  useEffect(() => { fetchGames() }, [])

  const fetchGames = async () => {
    const res = await api.get('/games')
    setGames(res.data)
  }

  const apiRoot = api.defaults.baseURL ? api.defaults.baseURL.replace(/\/api\/?$/, '') : 'http://localhost:5000'

  return (
    <Grid container spacing={2}>
      {games.map(g => (
        <Grid item xs={12} sm={6} md={4} key={g.id}>
          <div className="card">
            <CardActionArea component={Link} to={`/games/${g.id}`}>
              {g.imageUrl && <img className="game-list-image" src={`${apiRoot}${g.imageUrl}`} alt={g.name} />}
              <CardContent>
                <Typography variant="h6">{g.name}</Typography>
              </CardContent>
            </CardActionArea>
            <CardContent>
              <div style={{ display: 'flex', gap: 8 }}>
                <Button component={Link} to={`/submit/${g.id}`} className="btn" sx={{ mt: 1 }}>Submit Score</Button>
                {g.url && (
                  <Button href={g.url} target="_blank" rel="noreferrer" sx={{ mt: 1 }}>
                    Play
                  </Button>
                )}
              </div>
            </CardContent>
          </div>
        </Grid>
      ))}
    </Grid>
  )
}
