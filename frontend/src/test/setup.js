import '@testing-library/jest-dom'
import { configure } from '@testing-library/react'
import { vi } from 'vitest'

// MUI dialogs keep the page aria-hidden until their close animation ends, and
// parallel runs on a loaded machine are slow: the defaults (1 s for findBy /
// waitFor, 5 s per test) make those tests flaky. Disabling
// react-transition-group instead breaks MUI v5 Modal cleanup (the page stays
// aria-hidden), so the timeouts are raised.
const ASYNC_QUERY_TIMEOUT_MS = 3000
const TEST_TIMEOUT_MS = 15000

configure({ asyncUtilTimeout: ASYNC_QUERY_TIMEOUT_MS })
vi.setConfig({ testTimeout: TEST_TIMEOUT_MS })
