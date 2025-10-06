import { useMemo, useState } from 'react'
import {
    Box,
    Button,
    Chip,
    Stack,
    TextField,
    ToggleButton,
    ToggleButtonGroup,
} from '@mui/material'
import { DataGrid, type GridColDef } from '@mui/x-data-grid'
import RefreshRoundedIcon from '@mui/icons-material/RefreshRounded'
import VisibilityRoundedIcon from '@mui/icons-material/VisibilityRounded'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { enqueueSnackbar } from 'notistack'

import { PageContainer } from '@/components/layout/PageContainer'
import { StandardGridToolbar } from '@/components/tables/StandardGridToolbar'
import {
    REVIEW_STATUS,
    REVIEW_STATUS_LABELS,
    type ReviewResponseModel,
    type ReviewStatus,
} from '@/features/cine-review/reviews/types'
import { fetchReviews, recalculateReviewScore } from '@/features/cine-review/reviews/services/reviews.api'
import { ReviewDetailDialog } from '@/features/cine-review/reviews/components/ReviewDetailDialog'

export function ReviewManagementPage() {
    const [statusFilter, setStatusFilter] = useState<ReviewStatus | 'all'>('all')
    const [userFilter, setUserFilter] = useState<string>('')
    const [movieFilter, setMovieFilter] = useState<string>('')
    const [paginationModel, setPaginationModel] = useState({ page: 0, pageSize: 25 })
    const [selectedReview, setSelectedReview] = useState<ReviewResponseModel | null>(null)
    const [detailOpen, setDetailOpen] = useState(false)
    const queryClient = useQueryClient()

    const reviewsQuery = useQuery({
        queryKey: ['reviews', statusFilter, userFilter, movieFilter, paginationModel],
        queryFn: () =>
            fetchReviews({
                status: statusFilter === 'all' ? undefined : statusFilter,
                userId: userFilter ? Number(userFilter) : undefined,
                tmdbMovieId: movieFilter ? Number(movieFilter) : undefined,
                page: paginationModel.page + 1,
                pageSize: paginationModel.pageSize,
            }),
    })

    const recalcMutation = useMutation({
        mutationFn: recalculateReviewScore,
        onSuccess: () => {
            enqueueSnackbar('Communication score recalculated', { variant: 'success' })
            queryClient.invalidateQueries({ queryKey: ['reviews'] })
        },
        onError: (error) => {
            const message = error instanceof Error ? error.message : 'Failed to recalculate score'
            enqueueSnackbar(message, { variant: 'error' })
        },
    })

    const columns = useMemo<GridColDef<ReviewResponseModel>[]>(
        () => [
            {
                field: 'tmdbMovieId',
                headerName: 'TMDB',
                width: 120,
            },
            {
                field: 'userName',
                headerName: 'User',
                flex: 1,
                minWidth: 200,
                valueGetter: ({ row }: { row: any }) => row.userName ?? `User #${row.userId}`,
            },
            {
                field: 'rating',
                headerName: 'Rating',
                width: 110,
            },
            {
                field: 'communicationScore',
                headerName: 'Comm Score',
                width: 140,
                valueFormatter: ({ value }) => Number(value).toFixed(2),
            },
            {
                field: 'status',
                headerName: 'Status',
                width: 140,
                renderCell: ({ value }) => (
                    <Chip
                        label={REVIEW_STATUS_LABELS[value as ReviewStatus]}
                        color={value === REVIEW_STATUS.Pending ? 'warning' : value === REVIEW_STATUS.Deleted ? 'default' : 'success'}
                        size="small"
                    />
                ),
            },
            {
                field: 'createdOnUtc',
                headerName: 'Created',
                width: 190,
                valueFormatter: ({ value }) => (value ? new Date(value).toLocaleString() : '—'),
            },
            {
                field: 'actions',
                headerName: 'Actions',
                width: 170,
                sortable: false,
                filterable: false,
                renderCell: ({ row }) => (
                    <Stack direction="row" spacing={1}>
                        <Button
                            size="small"
                            variant="outlined"
                            startIcon={<VisibilityRoundedIcon fontSize="small" />}
                            onClick={() => {
                                setSelectedReview(row)
                                setDetailOpen(true)
                            }}
                        >
                            View
                        </Button>
                        <Button
                            size="small"
                            variant="contained"
                            color="secondary"
                            startIcon={<RefreshRoundedIcon fontSize="small" />}
                            onClick={() => recalcMutation.mutate(row.id)}
                            disabled={recalcMutation.isPending}
                        >
                            Score
                        </Button>
                    </Stack>
                ),
            },
        ],
        [recalcMutation.isPending],
    )

    const rows = (reviewsQuery.data ?? []).map((review) => ({ ...review, id: review.id }))

    return (
        <PageContainer
            title="Reviews"
            subtitle="Monitor community sentiment and ensure high quality contributions."
            action={
                <Stack direction="row" spacing={1}>
                    <ToggleButtonGroup
                        value={statusFilter}
                        exclusive
                        onChange={(_, value) => {
                            if (value === null) return
                            setStatusFilter(value as ReviewStatus | 'all')
                        }}
                        size="small"
                    >
                        <ToggleButton value="all">All</ToggleButton>
                        {Object.entries(REVIEW_STATUS_LABELS).map(([key, label]) => (
                            <ToggleButton key={key} value={Number(key)}>
                                {label}
                            </ToggleButton>
                        ))}
                    </ToggleButtonGroup>
                </Stack>
            }
        >
            <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} mb={2}>
                <TextField
                    label="Filter by user id"
                    value={userFilter}
                    onChange={(event) => setUserFilter(event.target.value)}
                    type="number"
                />
                <TextField
                    label="Filter by TMDB id"
                    value={movieFilter}
                    onChange={(event) => setMovieFilter(event.target.value)}
                    type="number"
                />
            </Stack>

            <Box sx={{ height: 600 }}>
                <DataGrid
                    rows={rows}
                    columns={columns}
                    disableRowSelectionOnClick
                    paginationModel={paginationModel}
                    onPaginationModelChange={setPaginationModel}
                    pageSizeOptions={[10, 25, 50]}
                    rowCount={rows.length}
                    loading={reviewsQuery.isLoading || reviewsQuery.isFetching}
                    slots={{ toolbar: StandardGridToolbar }}
                />
            </Box>

            <ReviewDetailDialog open={detailOpen} review={selectedReview} onClose={() => setDetailOpen(false)} />
        </PageContainer>
    )
}
