import { useEffect, useMemo } from 'react'
import {
  Box,
  Button,
  Container,
  Link,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
import { useLocation, useNavigate } from 'react-router-dom'
import GoogleIcon from '@mui/icons-material/Google'
import { useAuth } from '@/hooks/useAuth'

interface LocationState {
  from?: string
  reason?: string
}

export function LoginPage() {
  const location = useLocation()
  const navigate = useNavigate()
  const { auth } = useAuth()
  const redirectTo = useMemo(() => (location.state as LocationState | null)?.from ?? '/', [location.state])

  // If already authenticated, redirect
  useEffect(() => {
    if (auth.status === 'authenticated') {
      navigate(redirectTo, { replace: true })
    }
  }, [auth.status, navigate, redirectTo])

  const handleGoogleSignIn = () => {
    const baseUrl = import.meta.env.VITE_IDENTITY_API_BASE_URL ?? 'http://localhost:5123'
    const redirectUrl = `${baseUrl}/api/account/authenticate?redirectClientUrl=${encodeURIComponent(window.location.origin)}`
    window.location.href = redirectUrl
  }

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        bgcolor: (theme) => (theme.palette.mode === 'light' ? '#f3f5fb' : '#050b18'),
        py: 6,
      }}
    >
      <Container maxWidth="sm">
        <Paper
          elevation={0}
          sx={{
            borderRadius: 5,
            p: { xs: 4, md: 6 },
            border: (theme) => `1px solid ${theme.palette.divider}`,
          }}
        >
          <Stack spacing={3}>
            <Stack spacing={1} textAlign="center">
              <Typography variant="h3" fontWeight={700}>
                CineReview CMS
              </Typography>
              <Typography variant="body1" color="text.secondary">
                Sign in using your Google account with administrator or partner credentials.
              </Typography>
              {location.state && (location.state as LocationState)?.reason ? (
                <Typography variant="body2" color="error">
                  {(location.state as LocationState).reason}
                </Typography>
              ) : null}
            </Stack>

            <Button
              variant="contained"
              size="large"
              disableElevation
              startIcon={<GoogleIcon />}
              onClick={handleGoogleSignIn}
              sx={{ borderRadius: 3, py: 1.5 }}
            >
              Sign in with Google
            </Button>

            <Typography variant="caption" color="text.secondary" textAlign="center">
              By signing in you agree to follow CineReview operational guidelines.
            </Typography>

            <Typography variant="caption" textAlign="center">
              Need access?{' '}
              <Link href="mailto:support@cinereview.app?subject=CineReview%20CMS%20Access">
                Contact platform engineering
              </Link>
            </Typography>
          </Stack>
        </Paper>
      </Container>
    </Box>
  )
}
