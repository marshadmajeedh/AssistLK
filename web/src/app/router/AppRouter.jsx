import {
	BrowserRouter,
	Navigate,
	Route,
	Routes,
} from "react-router-dom";

import LoginPage from "../../features/auth/pages/LoginPage";

import ProtectedRoute from "../../shared/auth/ProtectedRoute";
import AdminLayout from "../../shared/layouts/AdminLayout";
import PlaceholderPage from "../../shared/components/PlaceholderPage";

function AppRouter() {
	return (
		<BrowserRouter>
			<Routes>
				<Route
					path="/login"
					element={<LoginPage />}
				/>

				<Route
					element={
						<ProtectedRoute
							allowedRoles={["Admin"]}
						/>
					}
				>
					<Route element={<AdminLayout />}>
						<Route
							path="/dashboard"
							element={
								<PlaceholderPage title="Dashboard" />
							}
						/>

						<Route
							path="/service-requests"
							element={
								<PlaceholderPage title="Service Requests" />
							}
						/>

						<Route
							path="/providers"
							element={
								<PlaceholderPage title="Providers" />
							}
						/>

						<Route
							path="/quotations"
							element={
								<PlaceholderPage title="Quotations" />
							}
						/>

						<Route
							path="/service-tracking"
							element={
								<PlaceholderPage title="Service Tracking" />
							}
						/>

						<Route
							path="/ai-workflows"
							element={
								<PlaceholderPage title="AI Workflows" />
							}
						/>
					</Route>
				</Route>

				<Route
					path="/unauthorized"
					element={<PlaceholderPage title="Unauthorized" />}
				/>

				<Route
					path="/"
					element={<Navigate to="/dashboard" replace />}
				/>

				<Route
					path="*"
					element={<PlaceholderPage title="Page Not Found" />}
				/>
			</Routes>
		</BrowserRouter>
	);
}

export default AppRouter;
