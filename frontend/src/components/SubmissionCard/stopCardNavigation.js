/** Keeps a click inside the card from following the card's own link. */
export default function stopCardNavigation(event) {
  event.preventDefault()
  event.stopPropagation()
}
