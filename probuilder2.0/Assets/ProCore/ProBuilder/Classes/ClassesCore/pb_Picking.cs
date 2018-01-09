﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProBuilder.Core
{
	public struct pb_PickerOptions
	{
		/// <summary>
		/// Which faces should be culled when picking elements. Set to Back to select only visible elements, None to select any element regardless of visibility.
		/// </summary>
		public pb_Culling culling;

		/// <summary>
		/// Require elements to be completely encompassed by the rect selection (Complete) or only touched (Partial).
		/// Does not apply to vertex picking.
		/// </summary>
		public pb_RectSelectMode rectSelectMode;

		static readonly pb_PickerOptions k_Default = new pb_PickerOptions()
		{
			culling = pb_Culling.Back,
			rectSelectMode = pb_RectSelectMode.Partial,
		};

		public static pb_PickerOptions Default
		{
			get { return k_Default; }
		}
	}

	class pb_Picking
	{
		/// <summary>
		/// Pick the vertex indices contained within a rect.
		/// </summary>
		/// <param name="cam"></param>
		/// <param name="rect">Rect is in GUI space, where 0,0 is top left of screen, width = cam.pixelWidth / pointsPerPixel.</param>
		/// <param name="selectable">The objects to hit test.</param>
		/// <param name="options">Culling options.</param>
		/// <param name="pixelsPerPoint">Scale the render texture to match rect coordinates. Generally you'll just pass in EditorGUIUtility.pointsPerPixel.</param>
		/// <returns></returns>
		public static Dictionary<pb_Object, HashSet<int>> PickVerticesInRect(
			Camera cam,
			Rect rect,
			IEnumerable<pb_Object> selectable,
			pb_PickerOptions options,
			float pixelsPerPoint = 1f)
		{
			if ((int) options.culling > 0)
			{
				// todo pb_Culling.Front is not respected
				// todo Use pb_SelectionPicker path for culled and unculled paths (set face culling in object shader)
				return pb_SelectionPicker.PickVerticesInRect(
					cam,
					rect,
					selectable,
					(int) (cam.pixelWidth / pixelsPerPoint),
					(int) (cam.pixelHeight / pixelsPerPoint));
			}
			else
			{
				var selected = new Dictionary<pb_Object, HashSet<int>>();

				foreach(var pb in selectable)
				{
					if(!pb.isSelectable)
						continue;

					pb_IntArray[] sharedIndices = pb.sharedIndices;
					HashSet<int> inRect = new HashSet<int>();
					Vector3[] positions = pb.vertices;
					var trs = pb.transform;
					float pixelHeight = cam.pixelHeight;

					for(int n = 0; n < sharedIndices.Length; n++)
					{
						Vector3 v = trs.TransformPoint(positions[sharedIndices[n][0]]);
						Vector3 p = cam.WorldToScreenPoint(v);

						if (p.z < cam.nearClipPlane)
							continue;

						p.x /= pixelsPerPoint;
						p.y = (pixelHeight - p.y) / pixelsPerPoint;

						if(rect.Contains(p))
							inRect.Add(n);
					}

					selected.Add(pb, inRect);
				}

				return selected;
			}
		}

		/// <summary>
		/// Pick faces contained within rect.
		/// </summary>
		/// <param name="cam"></param>
		/// <param name="rect">Rect is in GUI space, where 0,0 is top left of screen, width = cam.pixelWidth / pointsPerPixel.</param>
		/// <param name="selectable"></param>
		/// <param name="options"></param>
		/// <param name="pixelsPerPoint">Scale the render texture to match rect coordinates. Generally you'll just pass in EditorGUIUtility.pixelsPerPoint.</param>
		/// <returns></returns>
		public static Dictionary<pb_Object, HashSet<pb_Face>> PickFacesInRect(
			Camera cam,
			Rect rect,
			IEnumerable<pb_Object> selectable,
			pb_PickerOptions options,
			float pixelsPerPoint = 1f)
		{
			if (options.culling == pb_Culling.Back && options.rectSelectMode == pb_RectSelectMode.Partial)
			{
				// todo handle both culling modes here
				return pb_SelectionPicker.PickFacesInRect(
					cam,
					rect,
					selectable,
					(int) (cam.pixelWidth / pixelsPerPoint),
					(int) (cam.pixelHeight / pixelsPerPoint));
			}

			var selected = new Dictionary<pb_Object, HashSet<pb_Face>>();

			foreach(var pb in selectable)
			{
				if (!pb.isSelectable)
					continue;

				HashSet<pb_Face> selectedFaces = new HashSet<pb_Face>();
				Transform trs = pb.transform;
				Vector3[] positions = pb.vertices;
				Vector3[] screenPoints = new Vector3[pb.vertexCount];

				for(int nn = 0; nn < pb.vertexCount; nn++)
					screenPoints[nn] = cam.ScreenToGuiPoint(cam.WorldToScreenPoint(trs.TransformPoint(positions[nn])), pixelsPerPoint);

				for(int n = 0; n < pb.faces.Length; n++)
				{
					pb_Face face = pb.faces[n];

					// rect select = complete
					if(options.rectSelectMode == pb_RectSelectMode.Complete)
					{
						// face is behind the camera
						if(screenPoints[face.indices[0]].z < cam.nearClipPlane)
							continue;

						// only check the first index per quad, and if it checks out, then check every other point
						if(rect.Contains(screenPoints[face.indices[0]]))
						{
							bool nope = false;

							for(int q = 1; q < face.distinctIndices.Length; q++)
							{
								int index = face.distinctIndices[q];

								if(screenPoints[index].z < cam.nearClipPlane || !rect.Contains(screenPoints[index]))
								{
									nope = true;
									break;
								}
							}

							if(!nope)
							{
								// todo occlusion testing here is super slow - use pb_SelectionPicker path
								if( options.culling == pb_Culling.None ||
									!pb_HandleUtility.PointIsOccluded(cam, pb, trs.TransformPoint(pb_Math.Average(positions, face.distinctIndices))))
								{
									selectedFaces.Add(face);
								}
							}
						}
					}
					// rect select = partial
					else
					{
						pb_Bounds2D poly = new pb_Bounds2D(screenPoints, face.edges);
						bool overlaps = false;

						if( poly.Intersects(rect) )
						{
							// if rect contains one point of polygon, it overlaps
							for (int nn = 0; nn < face.distinctIndices.Length && !overlaps; nn++)
							{
								Vector3 p = screenPoints[face.distinctIndices[nn]];
								overlaps = p.z > cam.nearClipPlane && rect.Contains(p);
							}

							// if polygon contains one point of rect, it overlaps. otherwise check for edge intersections
							if(!overlaps)
							{
								Vector2 tl = new Vector2(rect.xMin, rect.yMax);
								Vector2 tr = new Vector2(rect.xMax, rect.yMax);
								Vector2 bl = new Vector2(rect.xMin, rect.yMin);
								Vector2 br = new Vector2(rect.xMax, rect.yMin);

								overlaps = pb_Math.PointInPolygon(screenPoints, poly, face.edges, tl);
								if(!overlaps) overlaps = pb_Math.PointInPolygon(screenPoints, poly, face.edges, tr);
								if(!overlaps) overlaps = pb_Math.PointInPolygon(screenPoints, poly, face.edges, br);
								if(!overlaps) overlaps = pb_Math.PointInPolygon(screenPoints, poly, face.edges, bl);

								// if any polygon edge intersects rect
								for(int nn = 0; nn < face.edges.Length && !overlaps; nn++)
								{
									if( pb_Math.GetLineSegmentIntersect(tr, tl, screenPoints[face.edges[nn].x], screenPoints[face.edges[nn].y]) )
										overlaps = true;
									else
									if( pb_Math.GetLineSegmentIntersect(tl, bl, screenPoints[face.edges[nn].x], screenPoints[face.edges[nn].y]) )
										overlaps = true;
									else
									if( pb_Math.GetLineSegmentIntersect(bl, br, screenPoints[face.edges[nn].x], screenPoints[face.edges[nn].y]) )
										overlaps = true;
									else
									if( pb_Math.GetLineSegmentIntersect(br, tl, screenPoints[face.edges[nn].x], screenPoints[face.edges[nn].y]) )
										overlaps = true;
								}
							}
						}

						// don't test occlusion since that case is handled special
						if(overlaps)
							selectedFaces.Add(face);
					}
				}

				selected.Add(pb, selectedFaces);
			}

			return selected;
		}
	}
}