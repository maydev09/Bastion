import { Navigate } from "react-router";
import { defineModule } from "@shared/moduleDefinition";
import { CollectionPage } from "./CollectionPage";
import { OfferDetail } from "./OfferDetail";
import { OfferList } from "./OfferList";
import en from "./locales/en.json";
import pl from "./locales/pl.json";

export const housingModule = defineModule({
  name: "Housing",
  routePrefix: "housing",
  icon: "house",
  namespace: "housing",
  locales: { pl, en },
  routes: [
    { path: "", element: <Navigate to="offers" replace /> },
    { path: "offers", element: <OfferList /> },
    { path: "offers/:id", element: <OfferDetail /> },
    { path: "collection", element: <CollectionPage /> },
  ],
  pages: [
    { path: "offers", title: "nav.offers", icon: "list" },
    { path: "collection", title: "nav.collection", icon: "download" },
  ],
});
