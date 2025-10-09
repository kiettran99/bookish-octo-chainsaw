import { useCallback, useMemo, useState } from 'react'
import {
  Box,
  Chip,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material'
import { DataGrid, type GridColDef, type GridFilterModel } from '@mui/x-data-grid'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { enqueueSnackbar } from 'notistack'

import { PageContainer } from '@/components/layout/PageContainer'
import { StandardGridToolbar } from '@/components/tables/StandardGridToolbar'
import { fetchUsers, updateUser } from '@/features/identity/users/services/users.api'
import type { UserPagingModel, UserPagingResponse, UserUpdateRequestModel } from '@/features/identity/users/types'
import { UserDetailDrawer } from '@/features/identity/users/components/UserDetailDrawer'
import { fetchRoles } from '@/features/identity/roles/services/roles.api'

const DEFAULT_PAGE_SIZE = 10

export function UserListPage() {
  const [paginationModel, setPaginationModel] = useState({ page: 0, pageSize: DEFAULT_PAGE_SIZE })
  const [quickFilter, setQuickFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'banned'>('all')
  const [selectedUser, setSelectedUser] = useState<UserPagingModel | null>(null)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const queryClient = useQueryClient()

  // Date filters: default to 60 days ago to today
  const getDefaultDateFrom = () => {
    const date = new Date()
    date.setDate(date.getDate() - 60)
    return date.toISOString().split('T')[0]
  }
  
  const getDefaultDateTo = () => {
    return new Date().toISOString().split('T')[0]
  }

  const [dateFrom, setDateFrom] = useState<string>(getDefaultDateFrom())
  const [dateTo, setDateTo] = useState<string>(getDefaultDateTo())

  const { data: roles = [] } = useQuery({
    queryKey: ['roles'],
    queryFn: fetchRoles,
    staleTime: 5 * 60 * 1000,
  })

  const userQueryKey = useMemo(
    () => [
      'users',
      {
        page: paginationModel.page,
        pageSize: paginationModel.pageSize,
        quickFilter,
        status: statusFilter,
        dateFrom,
        dateTo,
      },
    ],
    [paginationModel.page, paginationModel.pageSize, quickFilter, statusFilter, dateFrom, dateTo],
  )

  const usersQuery = useQuery<UserPagingResponse>({
    queryKey: userQueryKey,
    queryFn: () =>
      fetchUsers({
        pageNumber: paginationModel.page + 1,
        pageSize: paginationModel.pageSize,
        searchTerm: quickFilter || undefined,
        isBanned: statusFilter === 'banned' ? true : statusFilter === 'active' ? false : undefined,
        dateFrom: dateFrom || undefined,
        dateTo: dateTo || undefined,
      }),
    placeholderData: (previousData) => previousData,
  })

  const mutation = useMutation({
    mutationFn: ({ userId, payload }: { userId: number; payload: UserUpdateRequestModel }) =>
      updateUser(userId, payload),
    onSuccess: () => {
      enqueueSnackbar('User updated successfully', { variant: 'success' })
      queryClient.invalidateQueries({ queryKey: ['users'] })
      setDrawerOpen(false)
    },
    onError: (error) => {
      const message = error instanceof Error ? error.message : 'Failed to update user'
      enqueueSnackbar(message, { variant: 'error' })
    },
  })

  const rows = (usersQuery.data?.data ?? []).map((row) => ({ ...row, id: row.id }))

  const totalRows = usersQuery.data?.rowNum ?? rows.length

  const handleFilterModelChange = useCallback((model: GridFilterModel) => {
    const value = model.quickFilterValues?.[0] ?? ''
    setQuickFilter(value)
  }, [])

  const handleStatusChange = (_: React.MouseEvent<HTMLElement>, next: 'all' | 'active' | 'banned' | null) => {
    if (!next) return
    setStatusFilter(next)
  }

  const handleManageUser = (user: UserPagingModel) => {
    setSelectedUser(user)
    setDrawerOpen(true)
  }

  const columns = useMemo<GridColDef<UserPagingModel>[]>(
    () => [
      {
        field: 'fullName',
        headerName: 'Full name',
        flex: 1,
        minWidth: 200,
        valueGetter: (_, row) => row.fullName ?? row.userName,
      },
      {
        field: 'email',
        headerName: 'Email',
        flex: 1,
        minWidth: 220,
      },
      {
        field: 'roles',
        headerName: 'Roles',
        flex: 1,
        minWidth: 200,
        renderCell: ({ value }) => {
          if (!value) return <Typography variant="body2">—</Typography>
          const items = String(value)
            .split(',')
            .map((role) => role.trim())
            .filter(Boolean)

          return (
            <Stack direction="row" spacing={1} flexWrap="wrap">
              {items.map((role) => (
                <Chip key={role} size="small" label={role} color={role === 'Administrator' ? 'primary' : 'default'} />
              ))}
            </Stack>
          )
        },
      },
      {
        field: 'isBanned',
        headerName: 'Banned',
        width: 120,
        valueFormatter: ({ value }) => (value ? 'Yes' : 'No'),
      },
      {
        field: 'createdOnUtc',
        headerName: 'Created',
        width: 180,
        valueFormatter: ({ value }) => (value ? new Date(value).toLocaleString() : '—'),
      },
      {
        field: 'actions',
        headerName: 'Actions',
        width: 120,
        sortable: false,
        filterable: false,
        renderCell: ({ row }) => (
          <Chip
            label="Manage"
            color="primary"
            variant="outlined"
            onClick={() => handleManageUser(row)}
            sx={{ cursor: 'pointer' }}
          />
        ),
      },
    ],
    [],
  )

  return (
    <PageContainer
      title="Identity users"
      subtitle="Review member profiles and ban abusive accounts across CineReview ecosystems."
      action={
        <ToggleButtonGroup size="small" value={statusFilter} exclusive onChange={handleStatusChange}>
          <ToggleButton value="all">All</ToggleButton>
          <ToggleButton value="active">Active</ToggleButton>
          <ToggleButton value="banned">Banned</ToggleButton>
        </ToggleButtonGroup>
      }
    >
      <Stack spacing={2} mb={2}>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
          <TextField
            label="Date from"
            value={dateFrom}
            onChange={(event) => setDateFrom(event.target.value)}
            type="date"
            size="small"
            InputLabelProps={{ shrink: true }}
            sx={{ flex: 1 }}
          />
          <TextField
            label="Date to"
            value={dateTo}
            onChange={(event) => setDateTo(event.target.value)}
            type="date"
            size="small"
            InputLabelProps={{ shrink: true }}
            sx={{ flex: 1 }}
          />
        </Stack>
      </Stack>

      <Box sx={{ height: 600, width: '100%' }}>
        <DataGrid
          rows={rows}
          columns={columns}
          disableRowSelectionOnClick
          loading={usersQuery.isLoading || usersQuery.isFetching}
          paginationMode="server"
          rowCount={totalRows}
          paginationModel={paginationModel}
          onPaginationModelChange={setPaginationModel}
          pageSizeOptions={[10, 25, 50]}
          filterMode="server"
          onFilterModelChange={handleFilterModelChange}
          slots={{ toolbar: StandardGridToolbar }}
        />
      </Box>

      <UserDetailDrawer
        open={drawerOpen}
        user={selectedUser}
        availableRoles={roles}
        isSubmitting={mutation.isPending}
        onClose={() => setDrawerOpen(false)}
        onSubmit={(payload) => {
          if (!selectedUser) return
          mutation.mutate({ userId: selectedUser.id, payload })
        }}
      />
    </PageContainer>
  )
}
