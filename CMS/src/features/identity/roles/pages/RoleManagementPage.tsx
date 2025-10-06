import { useState } from 'react'
import {
  Box,
  Button,
  IconButton,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import DeleteRoundedIcon from '@mui/icons-material/DeleteRounded'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { enqueueSnackbar } from 'notistack'

import { PageContainer } from '@/components/layout/PageContainer'
import { ConfirmDialog } from '@/components/feedback/ConfirmDialog'
import { RoleDialog } from '@/features/identity/roles/components/RoleDialog'
import { createRole, deleteRole, fetchRoles, updateRole } from '@/features/identity/roles/services/roles.api'
import type { RoleModel } from '@/features/identity/roles/types'

export function RoleManagementPage() {
  const queryClient = useQueryClient()
  const [dialogOpen, setDialogOpen] = useState(false)
  const [dialogMode, setDialogMode] = useState<'create' | 'edit'>('create')
  const [selectedRole, setSelectedRole] = useState<RoleModel | null>(null)
  const [confirmOpen, setConfirmOpen] = useState(false)

  const rolesQuery = useQuery({ queryKey: ['roles'], queryFn: fetchRoles })

  const createMutation = useMutation({
    mutationFn: createRole,
    onSuccess: () => {
      enqueueSnackbar('Role created', { variant: 'success' })
      queryClient.invalidateQueries({ queryKey: ['roles'] })
      setDialogOpen(false)
    },
    onError: (error) => {
      const message = error instanceof Error ? error.message : 'Failed to create role'
      enqueueSnackbar(message, { variant: 'error' })
    },
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => updateRole(id, { name }),
    onSuccess: () => {
      enqueueSnackbar('Role updated', { variant: 'success' })
      queryClient.invalidateQueries({ queryKey: ['roles'] })
      setDialogOpen(false)
    },
    onError: (error) => {
      const message = error instanceof Error ? error.message : 'Failed to update role'
      enqueueSnackbar(message, { variant: 'error' })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: deleteRole,
    onSuccess: () => {
      enqueueSnackbar('Role deleted', { variant: 'success' })
      queryClient.invalidateQueries({ queryKey: ['roles'] })
      setConfirmOpen(false)
    },
    onError: (error) => {
      const message = error instanceof Error ? error.message : 'Failed to delete role'
      enqueueSnackbar(message, { variant: 'error' })
    },
  })

  const handleOpenCreate = () => {
    setDialogMode('create')
    setSelectedRole(null)
    setDialogOpen(true)
  }

  const handleOpenEdit = (role: RoleModel) => {
    setDialogMode('edit')
    setSelectedRole(role)
    setDialogOpen(true)
  }

  const handleDeleteRole = (role: RoleModel) => {
    setSelectedRole(role)
    setConfirmOpen(true)
  }

  const roles = rolesQuery.data ?? []

  return (
    <PageContainer
      title="Roles"
      subtitle="Assign granular access to platform features for administrators and partners."
      action={
        <Button
          variant="contained"
          startIcon={<AddRoundedIcon />}
          onClick={handleOpenCreate}
          disableElevation
        >
          New role
        </Button>
      }
    >
      <Paper sx={{ borderRadius: 4, overflow: 'hidden' }}>
        <Box sx={{ px: 3, py: 2, borderBottom: (theme) => `1px solid ${theme.palette.divider}` }}>
          <Typography variant="subtitle1" fontWeight={600}>
            Defined roles
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Roles sync instantly with Identity API consumers.
          </Typography>
        </Box>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>Normalized</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {roles.map((role) => (
              <TableRow key={role.id} hover>
                <TableCell>{role.name}</TableCell>
                <TableCell>{role.normalizedName}</TableCell>
                <TableCell align="right">
                  <Stack direction="row" spacing={1} justifyContent="flex-end">
                    <IconButton
                      size="small"
                      color="primary"
                      onClick={() => handleOpenEdit(role)}
                      aria-label="Edit role"
                    >
                      <EditRoundedIcon fontSize="small" />
                    </IconButton>
                    <IconButton
                      size="small"
                      color="error"
                      onClick={() => handleDeleteRole(role)}
                      aria-label="Delete role"
                    >
                      <DeleteRoundedIcon fontSize="small" />
                    </IconButton>
                  </Stack>
                </TableCell>
              </TableRow>
            ))}
            {roles.length === 0 && !rolesQuery.isLoading ? (
              <TableRow>
                <TableCell colSpan={3} align="center">
                  <Typography variant="body2" color="text.secondary">
                    No roles defined yet.
                  </Typography>
                </TableCell>
              </TableRow>
            ) : null}
          </TableBody>
        </Table>
      </Paper>

      <RoleDialog
        open={dialogOpen}
        mode={dialogMode}
        role={selectedRole}
        isSubmitting={createMutation.isPending || updateMutation.isPending}
        onClose={() => setDialogOpen(false)}
        onSubmit={(name) => {
          if (dialogMode === 'create') {
            createMutation.mutate({ name })
          } else if (selectedRole) {
            updateMutation.mutate({ id: selectedRole.id, name })
          }
        }}
      />

      <ConfirmDialog
        open={confirmOpen}
        title="Delete role"
        content={`This action will remove the role "${selectedRole?.name}" from the platform.`}
        isLoading={deleteMutation.isPending}
        onClose={() => setConfirmOpen(false)}
        onConfirm={() => {
          if (selectedRole) {
            deleteMutation.mutate(selectedRole.id)
          }
        }}
      />
    </PageContainer>
  )
}
