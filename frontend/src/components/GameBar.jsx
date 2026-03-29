import React, { useRef, useState, useEffect } from 'react'
import './gamebar.css'
import { Link } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'

export default function GameBar() {
  const { user, logout, fetchMe } = useAuth()

  // Real XP data from the authenticated user object
  const level = user?.level ?? 1
  const xpInto = user?.xpIntoLevel ?? 0
  const xpToNext = user?.xpToNextLevel ?? 100
  // xpForLevel is the total XP bucket for the current level (e.g. 100 for level 1)
  const xpForLevel = xpInto + xpToNext
  const streak = user?.streak ?? 0
  const progress = xpForLevel > 0 ? Math.min(100, Math.round((xpInto / xpForLevel) * 100)) : 0

  // Flash "+N XP" whenever totalXp increases (e.g. after a submission)
  const prevXpRef = useRef(null)
  const gainTimerRef = useRef(null)
  const [gainDisplay, setGainDisplay] = useState(null)

  useEffect(() => {
    if (!user) { prevXpRef.current = null; return }
    const current = user.totalXp ?? 0
    if (prevXpRef.current !== null && current > prevXpRef.current) {
      const gained = current - prevXpRef.current
      setGainDisplay(gained)
      clearTimeout(gainTimerRef.current)
      gainTimerRef.current = setTimeout(() => setGainDisplay(null), 2500)
    }
    prevXpRef.current = current
    return () => clearTimeout(gainTimerRef.current)
  }, [user?.totalXp])

  // Re-fetch when a level-up is detected so xpIntoLevel / xpToNextLevel are
  // accurate for the new level range (e.g. after navigating back from Submit).
  const prevLevelRef = useRef(null)
  useEffect(() => {
    if (!user) { prevLevelRef.current = null; return }
    const currentLevel = user.level ?? 1
    if (prevLevelRef.current !== null && currentLevel > prevLevelRef.current) {
      fetchMe()
    }
    prevLevelRef.current = currentLevel
  }, [user?.level])

  return (
    <header className="gamebar">
      <div className="gamebar-left">
        <Link to="/" style={{ display: 'flex', alignItems: 'center', textDecoration: 'none', color: 'inherit' }}>
          <div className="logo">🎮</div>
          <div>
            <div className="title">Daily Challenges</div>
            <div className="subtitle muted">Play, compete, repeat</div>
          </div>
        </Link>
      </div>

      <div className="gamebar-right">
        {user && (
          <div className="xp">
            <div className="xp-bar-wrapper">
              <div className="xp-bar-outer" aria-label={`XP progress: ${xpInto} of ${xpForLevel}`} aria-valuenow={progress} role="progressbar">
                <div className="xp-bar-inner" style={{ width: `${progress}%` }} />
              </div>
              {gainDisplay && (
                <span className="xp-gain" key={gainDisplay + Date.now()}>+{gainDisplay} XP</span>
              )}
            </div>
            <div className="xp-meta muted">
              Level <strong>{level}</strong> &bull; {xpInto.toLocaleString()}/{xpForLevel.toLocaleString()} XP
              {streak > 1 && <span className="xp-streak"> &nbsp;🔥 {streak}</span>}
            </div>
          </div>
        )}
        <div style={{ marginLeft: 12 }}>
          {user ? (
            <>
              {user.isAdmin && <Link to="/admin" style={{ marginRight: 8 }}>Admin</Link>}
              <span style={{ marginRight: 8 }}>Hello, {user.username}</span>
              <button onClick={() => logout()}>Logout</button>
            </>
          ) : (
            <>
              <Link to="/login">Login</Link> / <Link to="/register">Register</Link>
            </>
          )}
        </div>
      </div>
    </header>
  )
}

