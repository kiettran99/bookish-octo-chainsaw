import { useMemo, useState } from 'react'
import {
  Box,
  Button,
  Checkbox,
  Container,
  FormControlLabel,
  Link,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { useMutation } from '@tanstack/react-query'
import { useLocation, useNavigate } from 'react-router-dom'
import GoogleIcon from '@mui/icons-material/Google'
import KeyRoundedIcon from '@mui/icons-material/KeyRounded'
import { enqueueSnackbar } from 'notistack'

import { clientAuthenticate } from '@/features/auth/services/auth.api'
import { useAuth } from '@/hooks/useAuth'
import type { ClientAuthenticatePayload } from '@/features/auth/services/auth.api'

interface LocationState {
  from?: string
  reason?: string
}

const defaultPayload: ClientAuthenticatePayload = {
  providerAccountId: '',
  name: '',
  email: '',
  emailVerified: false,
  region: '',
  image: '',
}

export function LoginPage() {
  const location = useLocation()
  const navigate = useNavigate()
  const { loginWithResponse } = useAuth()
  const [form, setForm] = useState<ClientAuthenticatePayload>(defaultPayload)
  const redirectTo = useMemo(() => (location.state as LocationState | null)?.from ?? '/', [location.state])

  const { mutateAsync, isPending } = useMutation({
    mutationFn: clientAuthenticate,
    onSuccess: (response) => {
      loginWithResponse(response)
      enqueueSnackbar('Signed in successfully', { variant: 'success' })
      navigate(redirectTo, { replace: true })
    },
    onError: (error) => {
      const message = error instanceof Error ? error.message : 'Authentication failed'
      enqueueSnackbar(message, { variant: 'error' })
    },
  })

  const handleChange = (field: keyof ClientAuthenticatePayload) =>
    (event: React.ChangeEvent<HTMLInputElement>) => {
      const value =
        field === 'emailVerified' ? event.target.checked : (event.target.value as string)

      setForm((prev) => ({ ...prev, [field]: value }))
    }

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    await mutateAsync(form)
  }

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
          <Stack spacing={3} component="form" onSubmit={handleSubmit}>
            <Stack spacing={1} textAlign="center">
              <Typography variant="h3" fontWeight={700}>
                CineReview CMS
              </Typography>
              <Typography variant="body1" color="text.secondary">
                Sign in using administrator or partner credentials.
              </Typography>
              {location.state && (location.state as LocationState)?.reason ? (
                <Typography variant="body2" color="error">
                  {(location.state as LocationState).reason}
                </Typography>
              ) : null}
            </Stack>

            <Button
              variant="outlined"
              size="large"
              startIcon={<GoogleIcon />}
              onClick={handleGoogleSignIn}
              sx={{ borderRadius: 3, py: 1.2 }}
            >
              Sign in with Google
            </Button>

            <Stack spacing={2}>
              <Typography variant="subtitle1" fontWeight={600}>
                Or authenticate manually
              </Typography>
              <TextField
                required
                label="Provider Account Id"
                placeholder="Google subject identifier"
                value={form.providerAccountId}
                onChange={handleChange('providerAccountId')}
              />
              <TextField
                required
                label="Full Name"
                value={form.name}
                onChange={handleChange('name')}
              />
              <TextField
                required
                type="email"
                label="Email"
                value={form.email}
                onChange={handleChange('email')}
              />
              <TextField
                label="Avatar URL"
                value={form.image}
                onChange={handleChange('image')}
              />
              <TextField
                label="Region"
                value={form.region}
                onChange={handleChange('region')}
              />
              <FormControlLabel
                control={
                  <Checkbox
                    checked={form.emailVerified ?? false}
                    onChange={handleChange('emailVerified')}
                  />
                }
                label="Email has been verified"
              />
            </Stack>

            <Button
              type="submit"
              variant="contained"
              size="large"
              disableElevation
              startIcon={<KeyRoundedIcon />}
              disabled={isPending}
              sx={{ borderRadius: 3, py: 1.2 }}
            >
              {isPending ? 'Signing in…' : 'Sign in'}
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
