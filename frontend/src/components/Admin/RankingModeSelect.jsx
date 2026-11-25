import React from 'react'

export default function RankingModeSelect({ value, onChange }) {
  return (
    <div>
      <label>Ranking mode: </label>
      <select value={value} onChange={e => onChange(e.target.value)}>
        <option value="highest">Highest (higher score wins)</option>
        <option value="lowest">Lowest (lower score wins)</option>
      </select>
    </div>
  )
}
